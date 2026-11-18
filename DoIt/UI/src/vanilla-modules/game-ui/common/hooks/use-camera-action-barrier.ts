import { getModuleExport } from '../../../../utils/vanilla-modules';

/**
 * The game's hook that blocks the camera's input actions for as long as the component calling it is
 * mounted, unless `isLifted`: a vanilla modal holds it so that nothing behind it moves the camera.
 * It holds the game's `input.cameraBarrier` event binding, which blocks the `Camera` action map
 * while anything listens to it.
 */
export const useCameraActionBarrier = getModuleExport<(isLifted?: boolean) => void>(
  'game-ui/common/hooks/use-camera-action-barrier.tsx',
  'useCameraActionBarrier',
  (value): value is (isLifted?: boolean) => void => typeof value == 'function',
  () => {
    // The camera stays free when the game's hook is missing.
  }
);
