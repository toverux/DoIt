// oxlint-disable no-console -- a CLI talks on stdout.
// oxlint-disable import/no-nodejs-modules -- a CLI script runs on Bun, not in the game.

// Orders the bridging effort from the Paradox Mods catalogue, through two queries:
//
// - `candidates [--top N]` lists the N most subscribed Cities: Skylines II code mods on which the
//   ledger holds no verdict yet, so a session picks the next mod to bridge among them. Which of them
//   is worth a bridge is the conversation's judgement, not the script's: the ledger records it.
// - `status` lists every bridged mod beside its current version on Paradox, and marks drift where
//   the version a bridge was written against is not the current one.
//
// The ledger is `DoIt/Actions/Sources/Bridges/mods.json`, keyed by Paradox mod id, one verdict per
// mod: `bridged` with the version the bridge was written against, `set-aside` with the reason, or
// `gap` with the reason and, where one exists, the link to the upstream proposal. A session edits it
// by hand when a verdict is reached; this script only reads it.
//
// The catalogue is read through the search call the Paradox Mods website makes, which answers
// without a login; a single mod is read through the detail call. Neither needs a token.
//
// Run it by hand, from the repo root, through `mise bridges <query>`.

import assert from 'node:assert/strict';
import { parseArgs } from 'node:util';

const API_URL = 'https://api.paradox-interactive.com';
const LEDGER_PATH = 'DoIt/Actions/Sources/Bridges/mods.json';
const DEFAULT_TOP = 10;
// The search call caps a page at 100 rows whatever the request asks.
const PAGE_SIZE = 100;
const REQUEST_TIMEOUT_MS = 10_000;
const DESCRIPTION_WIDTH = 80;

const USAGE = [
  `Usage:`,
  `  mise bridges candidates [--top N]  The N (default ${DEFAULT_TOP}) most subscribed code mods ` +
    `the ledger holds no verdict on yet.`,
  `  mise bridges status                Every bridged mod beside its current version on Paradox, ` +
    `drift marked.`
].join('\n');

type Verdict =
  | { readonly name: string; readonly status: 'bridged'; readonly version: string }
  | {
      readonly name: string;
      readonly status: 'set-aside' | 'gap';
      readonly reason: string;
      readonly link?: string;
    };

type Ledger = Readonly<Record<string, Verdict>>;

// The fields of a catalogue row the queries print, out of the many the search call returns.
interface CatalogueMod {
  readonly modId: string;
  readonly displayName: string;
  readonly author: string;
  readonly installedCount: number;
  // A mod can publish no version of its own: the search then answers null, and the detail call
  // omits it, so the queries fall back to the revision number Paradox counts uploads with.
  readonly userModVersion: string | null;
  readonly modVersion: string;
  readonly updated: string;
  readonly shortDescription: string;
}

interface ModDetail {
  readonly displayName: string;
  readonly userModVersion?: string;
  readonly modVersion: string;
  readonly latestUpdate: string;
}

await run();

async function run(): Promise<void> {
  try {
    // Strict, so a mistyped flag throws instead of running another mode.
    const { values: flags, positionals } = parseArgs({
      args: Bun.argv.slice(2),
      options: { top: { type: 'string', default: String(DEFAULT_TOP) } },
      allowPositionals: true
    });
    const [query] = positionals;

    if (query == 'candidates') {
      await printCandidates(parseTop(flags.top));
    } else if (query == 'status') {
      await printStatus();
    } else {
      console.error(USAGE);
      process.exitCode = 1;
    }
  } catch (error) {
    console.error(errorMessage(error));
    process.exitCode = 1;
  }
}

async function printCandidates(top: number): Promise<void> {
  const ledger = await readLedger();
  const rows: string[][] = [];

  for (let page = 0; rows.length < top; page++) {
    // oxlint-disable-next-line no-await-in-loop -- each page's position follows the previous one.
    const mods = await searchCodeMods(page);

    for (const [index, mod] of mods.entries()) {
      if (rows.length == top) {
        break;
      }

      if (Object.hasOwn(ledger, mod.modId)) {
        continue;
      }

      rows.push([
        String(page * PAGE_SIZE + index + 1),
        mod.modId,
        mod.displayName,
        mod.author,
        mod.installedCount.toLocaleString('en-US'),
        mod.userModVersion ?? mod.modVersion,
        formatDate(mod.updated),
        oneLine(mod.shortDescription)
      ]);
    }

    if (mods.length < PAGE_SIZE) {
      break;
    }
  }

  if (rows.length == 0) {
    console.info(`Every code mod of the catalogue has a verdict in ${LEDGER_PATH}.`);
    return;
  }

  console.info(
    formatTable(
      ['Rank', 'Id', 'Name', 'Author', 'Subscribers', 'Version', 'Updated', 'Description'],
      rows
    )
  );
}

async function printStatus(): Promise<void> {
  const ledger = await readLedger();
  const bridged = Object.entries(ledger).flatMap(([modId, verdict]) =>
    verdict.status == 'bridged' ? [{ modId, name: verdict.name, version: verdict.version }] : []
  );

  if (bridged.length == 0) {
    console.info(`No bridged mod in ${LEDGER_PATH} yet.`);
    return;
  }

  const results = await Promise.allSettled(bridged.map(bridge => fetchModDetail(bridge.modId)));
  const rows = bridged.map((bridge, index) => {
    const result = results[index];

    assert(result);

    if (result.status == 'rejected') {
      console.error(errorMessage(result.reason));

      return [bridge.modId, bridge.name, bridge.version, '?', '?', ''];
    }

    const detail = result.value;
    const current = detail.userModVersion ?? detail.modVersion;

    return [
      bridge.modId,
      detail.displayName,
      bridge.version,
      current,
      formatDate(detail.latestUpdate),
      current == bridge.version ? '' : 'drift'
    ];
  });

  console.info(formatTable(['Id', 'Name', 'Written against', 'Current', 'Updated', 'Drift'], rows));

  if (results.some(result => result.status == 'rejected')) {
    process.exitCode = 1;
  }
}

// The catalogue's code mods, most subscribed first, one page at a time. The website's own search
// call, which the API answers without a session.
async function searchCodeMods(page: number): Promise<CatalogueMod[]> {
  const response = await request('POST', '/mods?metadata=yes', {
    gameNames: ['cities_skylines_2'],
    tags: ['Code Mod'],
    sortBy: 'popularity',
    orderBy: 'desc',
    page,
    pageSize: PAGE_SIZE
  });

  const result = (await response.json()) as { mods: CatalogueMod[] };

  return result.mods;
}

async function fetchModDetail(modId: string): Promise<ModDetail> {
  // The `os` parameter is required, and Windows is the one platform every mod has a build for.
  const response = await request('GET', `/mods?modId=${modId}&os=Windows`);

  const result = (await response.json()) as { modDetail: ModDetail };

  return result.modDetail;
}

async function request(method: 'GET' | 'POST', path: string, body?: unknown): Promise<Response> {
  const call = `${method} ${path}`;
  let response: Response;

  try {
    response = await fetch(`${API_URL}${path}`, {
      method,
      ...(body == undefined
        ? {}
        : { headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) }),
      signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS)
    });
  } catch (error) {
    throw new Error(`Paradox Mods ${call}: ${errorMessage(error)}`, { cause: error });
  }

  if (!response.ok) {
    throw new Error(`Paradox Mods ${call}: ${response.status} ${await response.text()}`);
  }

  return response;
}

async function readLedger(): Promise<Ledger> {
  return (await Bun.file(LEDGER_PATH).json()) as Ledger;
}

function parseTop(value: string): number {
  const top = Number(value);

  if (!Number.isInteger(top) || top < 1) {
    throw new Error(`--top wants a positive integer, got "${value}".`);
  }

  return top;
}

// Plain text, columns padded to their widest cell, so the output pastes into a conversation as it
// is.
function formatTable(header: readonly string[], rows: ReadonlyArray<readonly string[]>): string {
  const widths = header.map((title, column) =>
    Math.max(title.length, ...rows.map(row => (row[column] ?? '').length))
  );

  return [
    line(header),
    line(widths.map(width => '-'.repeat(width))),
    ...rows.map(row => line(row))
  ].join('\n');

  function line(cells: readonly string[]): string {
    return cells
      .map((cell, column) => cell.padEnd(widths[column] ?? 0))
      .join('  ')
      .trimEnd();
  }
}

function formatDate(iso: string): string {
  return iso.slice(0, 'YYYY-MM-DD'.length);
}

function oneLine(text: string): string {
  const line = text.split('\n')[0]?.trim() ?? '';

  return line.length > DESCRIPTION_WIDTH ? `${line.slice(0, DESCRIPTION_WIDTH - 1)}…` : line;
}

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
