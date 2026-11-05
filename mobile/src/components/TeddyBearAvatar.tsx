import React, {
  forwardRef,
  useEffect,
  useImperativeHandle,
  useRef,
  useState,
} from 'react';
import { View, StyleSheet, Platform } from 'react-native';

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

export function clampLookValue(value: number): number {
  if (!Number.isFinite(value)) return 0;
  return Math.min(100, Math.max(0, Math.round(value)));
}

export function calculateLookFromText(text: string, maxChars = 32): number {
  if (!text) return 0;
  const clamped = Math.min(maxChars, Math.max(0, text.length));
  return clampLookValue((clamped / maxChars) * 100);
}

// Global script loader helper for @rive-app/canvas CDN in Expo Web preview
let riveScriptLoadingPromise: Promise<any> | null = null;
function loadRiveCanvasWeb(): Promise<any> {
  if (typeof window === 'undefined') return Promise.reject(new Error('Window undefined'));
  if ((window as any).rive) return Promise.resolve((window as any).rive);
  if (!riveScriptLoadingPromise) {
    riveScriptLoadingPromise = new Promise((resolve, reject) => {
      const script = document.createElement('script');
      script.src = 'https://unpkg.com/@rive-app/canvas@2.45.0';
      script.async = true;
      script.onload = () => {
        resolve((window as any).rive);
      };
      script.onerror = () => {
        reject(new Error('Failed to load @rive-app/canvas'));
      };
      document.head.appendChild(script);
    });
  }
  return riveScriptLoadingPromise;
}

export const TeddyBearAvatar = forwardRef<TeddyBearHandle, TeddyBearAvatarProps>(
  ({ variant = 'login' }, ref) => {
    const canvasRef = useRef<HTMLCanvasElement | null>(null);
    const riveRef = useRef<any>(null);
    const inputsRef = useRef<any>(null);
    const isMountedRef = useRef(true);

    const desiredStateRef = useRef<{
      mode: 'idle' | 'look' | 'hands_up';
      look: number;
    }>({
      mode: 'idle',
      look: 0,
    });

    const [hasLoadError, setHasLoadError] = useState(false);

    const applyDesiredState = () => {
      const inputs = inputsRef.current;
      if (!inputs || !isMountedRef.current) return;
      const { mode, look } = desiredStateRef.current;
      try {
        if (mode === 'hands_up') {
          if (inputs.check) inputs.check.value = false;
          if (inputs.look) inputs.look.value = 0;
          if (inputs.handsUp) inputs.handsUp.value = true;
        } else if (mode === 'look') {
          if (inputs.handsUp) inputs.handsUp.value = false;
          if (inputs.check) inputs.check.value = true;
          if (inputs.look) inputs.look.value = clampLookValue(look);
        } else {
          if (inputs.check) inputs.check.value = false;
          if (inputs.look) inputs.look.value = 0;
          if (inputs.handsUp) inputs.handsUp.value = false;
        }
      } catch {
        // ignore
      }
    };

    useImperativeHandle(ref, () => ({
      lookAt: (normalizedLook: number) => {
        const clamped = clampLookValue(normalizedLook);
        desiredStateRef.current = { mode: 'look', look: clamped };
        applyDesiredState();
      },
      setHandsUp: (up: boolean) => {
        desiredStateRef.current = {
          mode: up ? 'hands_up' : 'idle',
          look: desiredStateRef.current.look,
        };
        applyDesiredState();
      },
      setIdle: () => {
        desiredStateRef.current = { mode: 'idle', look: 0 };
        applyDesiredState();
      },
      triggerFail: () => {
        const inputs = inputsRef.current;
        if (!inputs?.fail) return;
        try {
          desiredStateRef.current = { mode: 'idle', look: 0 };
          applyDesiredState();
          inputs.fail.fire();
        } catch {
          // ignore
        }
      },
      triggerSuccess: () => {
        const inputs = inputsRef.current;
        if (!inputs?.success) return;
        try {
          desiredStateRef.current = { mode: 'idle', look: 0 };
          applyDesiredState();
          inputs.success.fire();
        } catch {
          // ignore
        }
      },
      reset: () => {
        desiredStateRef.current = { mode: 'idle', look: 0 };
        applyDesiredState();
      },
    }));

    useEffect(() => {
      isMountedRef.current = true;
      if (Platform.OS !== 'web') {
        return;
      }

      let riveInstance: any = null;
      let observer: ResizeObserver | null = null;

      const initRive = async () => {
        try {
          const rivePkg = await loadRiveCanvasWeb();
          const Rive = rivePkg.Rive;
          const Layout = rivePkg.Layout;
          const Fit = rivePkg.Fit;
          const Alignment = rivePkg.Alignment;

          const canvasEl = canvasRef.current;
          if (!canvasEl || !isMountedRef.current) return;

          const RIVE_SRC = '/animations/teddy_login_screen.riv';
          const ARTBOARD_NAME = 'Artboard';
          const STATE_MACHINE_NAME = 'LoginState';

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
            onLoad: () => {
              if (!isMountedRef.current) return;
              try {
                riveInstance.resizeDrawingSurfaceToCanvas();
                const rawInputs = riveInstance.stateMachineInputs(STATE_MACHINE_NAME);
                const byName = new Map<string, any>();
                if (Array.isArray(rawInputs)) {
                  for (const inp of rawInputs) {
                    if (inp && typeof inp.name === 'string') {
                      byName.set(inp.name, inp);
                    }
                  }
                }
                const inputs = {
                  check: byName.get('Check') ?? byName.get('check') ?? null,
                  look: byName.get('Look') ?? byName.get('look') ?? null,
                  handsUp: byName.get('hands_up') ?? byName.get('HandsUp') ?? null,
                  success: byName.get('success') ?? byName.get('Success') ?? null,
                  fail: byName.get('fail') ?? byName.get('Fail') ?? null,
                };
                inputsRef.current = inputs;
                applyDesiredState();
              } catch {
                // ignore
              }
            },
            onLoadError: () => {
              if (!isMountedRef.current) return;
              setHasLoadError(true);
            },
          });
          riveRef.current = riveInstance;

          if (typeof ResizeObserver !== 'undefined' && canvasEl) {
            observer = new ResizeObserver(() => {
              if (isMountedRef.current && riveRef.current) {
                try {
                  riveRef.current.resizeDrawingSurfaceToCanvas();
                } catch {
                  // ignore
                }
              }
            });
            observer.observe(canvasEl);
          }
        } catch {
          if (isMountedRef.current) {
            setHasLoadError(true);
          }
        }
      };

      initRive();

      return () => {
        isMountedRef.current = false;
        if (observer) {
          observer.disconnect();
        }
        if (riveInstance) {
          try {
            riveInstance.cleanup?.();
          } catch {
            // ignore
          }
        }
      };
    }, []);

    const isRegister = variant === 'register';
    const width = isRegister ? 169 : 192;
    const height = isRegister ? 142 : 162;

    if (Platform.OS !== 'web' || hasLoadError) {
      return (
        <View style={[styles.container, { width, height }]}>
          <View style={styles.fallbackBadge}>
            <View style={styles.fallbackS} />
          </View>
        </View>
      );
    }

    return (
      <View style={[styles.container, { width, height }]}>
        <canvas
          ref={canvasRef as any}
          width={width}
          height={height}
          style={{
            width: `${width}px`,
            height: `${height}px`,
            display: 'block',
            pointerEvents: 'none',
          }}
        />
      </View>
    );
  }
);

TeddyBearAvatar.displayName = 'TeddyBearAvatar';

const styles = StyleSheet.create({
  container: {
    alignSelf: 'center',
    alignItems: 'center',
    justifyContent: 'center',
    overflow: 'hidden',
  },
  fallbackBadge: {
    width: 56,
    height: 56,
    borderRadius: 18,
    backgroundColor: '#0F9D6E',
    alignItems: 'center',
    justifyContent: 'center',
  },
  fallbackS: {
    width: 24,
    height: 24,
  },
});
