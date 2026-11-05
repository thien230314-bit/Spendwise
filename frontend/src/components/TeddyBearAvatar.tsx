import React, {
  forwardRef,
  useEffect,
  useImperativeHandle,
  useRef,
  useState,
} from 'react';
import {
  Alignment,
  EventType,
  Fit,
  Layout,
  Rive,
  RuntimeLoader,
  StateMachineInput,
  StateMachineInputType,
} from '@rive-app/canvas';
import riveWasmUrl from '@rive-app/canvas/rive.wasm?url';

// Use bundled local WASM runtime from @rive-app/canvas to avoid external CDN failures
try {
  RuntimeLoader.setWasmUrl(riveWasmUrl);
  if ('suppressDeprecationWarnings' in Rive) {
    (
      Rive as unknown as { suppressDeprecationWarnings: string[] }
    ).suppressDeprecationWarnings = [
      'state-machine-inputs',
      'state-machines-param',
    ];
  }
} catch {
  // Ignore if already configured
}

const RIVE_SRC = '/animations/teddy_login_screen.riv';
const ARTBOARD_NAME = 'Artboard';
const STATE_MACHINE_NAME = 'LoginState';
const MAX_LOOK_CHARS = 32;

export interface TeddyBearHandle {
  lookAt: (normalizedLook: number) => void;
  setHandsUp: (up: boolean) => void;
  setIdle: () => void;
  triggerFail: () => void;
  triggerSuccess: () => void;
  reset: () => void;
}

export interface TeddyBearAvatarProps {
  variant?: 'login' | 'register';
}

interface VerifiedInputs {
  check: StateMachineInput | null;
  look: StateMachineInput;
  handsUp: StateMachineInput;
  success: StateMachineInput;
  fail: StateMachineInput;
}

export function clampLookValue(value: number): number {
  if (!Number.isFinite(value)) return 0;
  return Math.min(100, Math.max(0, Math.round(value)));
}

/**
 * Computes a normalized [0, 100] Look value from an input's caret position or text length.
 * Never passes raw text strings to Rive and never accumulates per keystroke.
 */
export function calculateLookFromInput(
  el: HTMLInputElement | null,
  preferCaret = false
): number {
  if (!el) return 0;
  const valLen = el.value ? el.value.length : 0;
  if (valLen === 0) return 0;

  const sel = typeof el.selectionStart === 'number' ? el.selectionStart : null;
  const charPos =
    sel !== null && (preferCaret || sel > 0) ? sel : valLen;

  const clampedChars = Math.min(MAX_LOOK_CHARS, Math.max(0, charPos));
  return clampLookValue((clampedChars / MAX_LOOK_CHARS) * 100);
}

export const TeddyBearAvatar = forwardRef<TeddyBearHandle, TeddyBearAvatarProps>(
  ({ variant = 'login' }, ref) => {
    const canvasRef = useRef<HTMLCanvasElement | null>(null);
    const riveRef = useRef<Rive | null>(null);
    const inputsRef = useRef<VerifiedInputs | null>(null);
    const isMountedRef = useRef(true);
    const reducedMotionRef = useRef(false);
    const pendingTriggerTimerRef = useRef<number | null>(null);

    // Track desired logical state in case commands arrive while Rive is still initializing
    const desiredStateRef = useRef<{
      mode: 'idle' | 'look' | 'hands_up';
      look: number;
    }>({
      mode: 'idle',
      look: 0,
    });

    const [hasLoadError, setHasLoadError] = useState(false);

    const clearPendingTriggerTimer = () => {
      if (pendingTriggerTimerRef.current !== null) {
        window.clearTimeout(pendingTriggerTimerRef.current);
        pendingTriggerTimerRef.current = null;
      }
    };

    const bindAndVerifyInputs = (riveInstance: Rive): VerifiedInputs | null => {
      const rawInputs = riveInstance.stateMachineInputs(STATE_MACHINE_NAME);
      if (!rawInputs || !Array.isArray(rawInputs) || rawInputs.length === 0) {
        return null;
      }

      const byName = new Map<string, StateMachineInput>();
      for (const inp of rawInputs) {
        if (inp && typeof inp.name === 'string') {
          byName.set(inp.name, inp);
        }
      }

      const check = byName.get('Check') ?? null;
      const look = byName.get('Look');
      const handsUp = byName.get('hands_up');
      const success = byName.get('success');
      const fail = byName.get('fail');

      if (
        !look ||
        look.type !== StateMachineInputType.Number ||
        !handsUp ||
        handsUp.type !== StateMachineInputType.Boolean ||
        !success ||
        success.type !== StateMachineInputType.Trigger ||
        !fail ||
        fail.type !== StateMachineInputType.Trigger
      ) {
        return null;
      }

      if (check && check.type !== StateMachineInputType.Boolean) {
        return null;
      }

      return { check, look, handsUp, success, fail };
    };

    const applyDesiredStateToInputs = () => {
      const inputs = inputsRef.current;
      if (!inputs || !isMountedRef.current) return;

      const { mode, look } = desiredStateRef.current;
      if (mode === 'hands_up') {
        if (inputs.check) inputs.check.value = false;
        inputs.look.value = 0;
        inputs.handsUp.value = true;
      } else if (mode === 'look') {
        inputs.handsUp.value = false;
        if (inputs.check) inputs.check.value = true;
        inputs.look.value = clampLookValue(look);
      } else {
        if (inputs.check) inputs.check.value = false;
        inputs.look.value = 0;
        inputs.handsUp.value = false;
      }
    };

    const fireOutcomeTrigger = (kind: 'fail' | 'success') => {
      if (!isMountedRef.current || reducedMotionRef.current) return;
      clearPendingTriggerTimer();

      const inputs = inputsRef.current;
      if (!inputs) return;

      const wasHandsUp = Boolean(inputs.handsUp.value);
      desiredStateRef.current = { mode: 'idle', look: 0 };
      if (inputs.check) inputs.check.value = false;
      inputs.look.value = 0;
      inputs.handsUp.value = false;

      const triggerInput = kind === 'success' ? inputs.success : inputs.fail;
      triggerInput.fire();

      // In teddy_login_screen.riv, fail/success transitions originate from 'idle'.
      // If the bear was covering its eyes (hands_up), lowering hands (hands_down -> idle)
      // takes ~1.05s before 'idle' can accept the trigger.
      if (wasHandsUp) {
        pendingTriggerTimerRef.current = window.setTimeout(() => {
          pendingTriggerTimerRef.current = null;
          if (!isMountedRef.current || reducedMotionRef.current) return;
          const activeInputs = inputsRef.current;
          if (!activeInputs) return;
          if (activeInputs.handsUp.value === false) {
            const target =
              kind === 'success' ? activeInputs.success : activeInputs.fail;
            target.fire();
          }
        }, 1100);
      }
    };

    useImperativeHandle(
      ref,
      () => ({
        lookAt(normalizedLook: number) {
          if (!isMountedRef.current) return;
          clearPendingTriggerTimer();
          const clamped = clampLookValue(normalizedLook);
          desiredStateRef.current = { mode: 'look', look: clamped };
          if (reducedMotionRef.current) return;
          applyDesiredStateToInputs();
        },
        setHandsUp(up: boolean) {
          if (!isMountedRef.current) return;
          clearPendingTriggerTimer();
          desiredStateRef.current = {
            mode: up ? 'hands_up' : 'idle',
            look: 0,
          };
          if (reducedMotionRef.current) return;
          applyDesiredStateToInputs();
        },
        setIdle() {
          if (!isMountedRef.current) return;
          desiredStateRef.current = { mode: 'idle', look: 0 };
          if (reducedMotionRef.current) return;
          applyDesiredStateToInputs();
        },
        triggerFail() {
          fireOutcomeTrigger('fail');
        },
        triggerSuccess() {
          fireOutcomeTrigger('success');
        },
        reset() {
          if (!isMountedRef.current) return;
          clearPendingTriggerTimer();
          desiredStateRef.current = { mode: 'idle', look: 0 };

          const riveInstance = riveRef.current;
          if (!riveInstance) return;

          try {
            riveInstance.reset({
              artboard: ARTBOARD_NAME,
              stateMachine: STATE_MACHINE_NAME,
              autoplay: !reducedMotionRef.current,
            } as Parameters<Rive['reset']>[0]);
            const verified = bindAndVerifyInputs(riveInstance);
            inputsRef.current = verified;
            if (verified) {
              if (verified.check) verified.check.value = false;
              verified.look.value = 0;
              verified.handsUp.value = false;
            }
            if (reducedMotionRef.current) {
              riveInstance.pause();
            }
          } catch {
            // Fallback to resetting input values if reset fails
            applyDesiredStateToInputs();
          }
        },
      }),
      []
    );

    useEffect(() => {
      isMountedRef.current = true;

      const mediaQuery =
        typeof window !== 'undefined' && window.matchMedia
          ? window.matchMedia('(prefers-reduced-motion: reduce)')
          : null;

      reducedMotionRef.current = Boolean(mediaQuery?.matches);

      const handleMotionChange = (e: MediaQueryListEvent) => {
        if (!isMountedRef.current) return;
        reducedMotionRef.current = e.matches;
        const r = riveRef.current;
        if (!r) return;
        if (e.matches) {
          clearPendingTriggerTimer();
          desiredStateRef.current = { mode: 'idle', look: 0 };
          applyDesiredStateToInputs();
          r.pause();
        } else {
          r.play(STATE_MACHINE_NAME);
          applyDesiredStateToInputs();
        }
      };

      mediaQuery?.addEventListener?.('change', handleMotionChange);

      const canvasEl = canvasRef.current;
      if (!canvasEl) {
        return () => {
          isMountedRef.current = false;
          clearPendingTriggerTimer();
          mediaQuery?.removeEventListener?.('change', handleMotionChange);
        };
      }

      let riveInstance: Rive | null = null;

      const handleLoad = () => {
        if (!isMountedRef.current || !riveInstance) return;
        const verified = bindAndVerifyInputs(riveInstance);
        if (!verified) {
          setHasLoadError(true);
          return;
        }
        inputsRef.current = verified;
        riveInstance.resizeDrawingSurfaceToCanvas();
        applyDesiredStateToInputs();

        if (reducedMotionRef.current) {
          window.requestAnimationFrame(() => {
            if (isMountedRef.current && riveRef.current) {
              riveRef.current.pause();
            }
          });
        }
      };

      const handleLoadError = () => {
        if (!isMountedRef.current) return;
        setHasLoadError(true);
      };

      try {
        riveInstance = new Rive({
          src: RIVE_SRC,
          canvas: canvasEl,
          artboard: ARTBOARD_NAME,
          stateMachine: STATE_MACHINE_NAME,
          autoplay: true,
          layout: new Layout({
            fit: Fit.Cover,
            alignment: Alignment.BottomCenter,
          }),
          onLoad: handleLoad,
          onLoadError: handleLoadError,
        } as ConstructorParameters<typeof Rive>[0]);
        riveRef.current = riveInstance;
      } catch {
        setHasLoadError(true);
      }

      const resizeObserver =
        typeof ResizeObserver !== 'undefined'
          ? new ResizeObserver(() => {
              if (isMountedRef.current && riveRef.current) {
                riveRef.current.resizeDrawingSurfaceToCanvas();
              }
            })
          : null;

      if (resizeObserver && canvasEl) {
        resizeObserver.observe(canvasEl);
      }

      return () => {
        isMountedRef.current = false;
        clearPendingTriggerTimer();
        mediaQuery?.removeEventListener?.('change', handleMotionChange);
        resizeObserver?.disconnect();
        if (riveInstance) {
          riveInstance.off(EventType.Load, handleLoad);
          riveInstance.off(EventType.LoadError, handleLoadError);
          riveInstance.cleanup();
        }
        riveRef.current = null;
        inputsRef.current = null;
      };
    }, []);

    // Stable dimensions (600:505 aspect ratio with Fit.Cover + BottomCenter trims empty top artboard space):
    // - Login: 192x162px -> visible bear is ~156px tall (~30% larger than previous 120px visible height)
    // - Register: 169x142px -> ~12.5% smaller than login for the longer registration form
    const sizeClasses =
      variant === 'register'
        ? 'h-[142px] w-[169px]'
        : 'h-[162px] w-[192px]';

    return (
      <div
        aria-hidden="true"
        data-testid="teddy-bear-slot"
        data-variant={variant}
        className={`relative mx-auto flex items-center justify-center overflow-hidden pointer-events-none select-none ${sizeClasses}`}
      >
        {hasLoadError ? (
          <div className="flex h-14 w-14 items-center justify-center rounded-[18px] bg-[var(--sw-accent-teal)] text-white shadow-md border border-teal-300/25">
            <span className="font-display text-[1.75rem] font-extrabold tracking-tight text-[#2DD4BF]">
              S
            </span>
          </div>
        ) : (
          <div className="relative mx-auto flex h-full w-full items-center justify-center pointer-events-none select-none">
            <canvas
              ref={canvasRef}
              tabIndex={-1}
              aria-hidden="true"
              className="block h-full w-full bg-transparent pointer-events-none select-none"
            />
          </div>
        )}
      </div>
    );
  }
);

TeddyBearAvatar.displayName = 'TeddyBearAvatar';
