import { ref } from "vue";

interface UseVisibilityAwarePollingOptions {
  task: () => Promise<unknown> | unknown;
  baseIntervalMs: number;
  hiddenIntervalMs?: number;
  maxBackoffMultiplier?: number;
  jitterRatio?: number;
  shouldPoll?: () => boolean;
  runImmediately?: boolean;
  onError?: (error: unknown) => void;
}

export const useVisibilityAwarePolling = ({
  task,
  baseIntervalMs,
  hiddenIntervalMs = baseIntervalMs,
  maxBackoffMultiplier = 8,
  jitterRatio = 0.1,
  shouldPoll,
  runImmediately = true,
  onError,
}: UseVisibilityAwarePollingOptions) => {
  const isActive = ref(false);
  let timeoutId: ReturnType<typeof setTimeout> | null = null;
  let inFlightTask: Promise<boolean> | null = null;
  let failureCount = 0;
  let visibilityListener: (() => void) | null = null;

  const clearScheduledTask = () => {
    if (timeoutId) {
      clearTimeout(timeoutId);
      timeoutId = null;
    }
  };

  const computeJitterMultiplier = () => {
    if (jitterRatio <= 0) {
      return 1;
    }
    const offset = (Math.random() * 2 - 1) * jitterRatio;
    return 1 + offset;
  };

  const getNextDelay = () => {
    const base = document.hidden ? hiddenIntervalMs : baseIntervalMs;
    const multiplier = Math.min(2 ** failureCount, maxBackoffMultiplier);
    const jitter = computeJitterMultiplier();
    return Math.max(0, Math.round(base * multiplier * jitter));
  };

  const runTask = async (): Promise<boolean> => {
    if (document.hidden) {
      return false;
    }

    if (shouldPoll && !shouldPoll()) {
      return false;
    }

    if (inFlightTask) {
      return inFlightTask;
    }

    inFlightTask = (async () => {
      try {
        await task();
        failureCount = 0;
        return true;
      } catch (error) {
        failureCount = Math.min(failureCount + 1, 6);
        onError?.(error);
        return false;
      } finally {
        inFlightTask = null;
      }
    })();

    return inFlightTask;
  };

  const scheduleNext = () => {
    if (!isActive.value) {
      return;
    }

    clearScheduledTask();
    timeoutId = setTimeout(async () => {
      if (!isActive.value) {
        return;
      }

      await runTask();
      scheduleNext();
    }, getNextDelay());
  };

  const runAndSchedule = async () => {
    await runTask();
    scheduleNext();
  };

  const start = () => {
    if (isActive.value) {
      return;
    }

    isActive.value = true;

    if (!visibilityListener) {
      visibilityListener = () => {
        if (!isActive.value) {
          return;
        }

        if (document.hidden) {
          clearScheduledTask();
          return;
        }

        void runAndSchedule();
      };
      document.addEventListener("visibilitychange", visibilityListener);
    }

    if (runImmediately && !document.hidden) {
      void runAndSchedule();
    } else if (!document.hidden) {
      scheduleNext();
    }
  };

  const stop = () => {
    isActive.value = false;
    clearScheduledTask();

    if (visibilityListener) {
      document.removeEventListener("visibilitychange", visibilityListener);
      visibilityListener = null;
    }
  };

  return {
    isActive,
    start,
    stop,
  };
};
