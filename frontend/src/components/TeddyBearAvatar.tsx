import React, { forwardRef, useImperativeHandle, useState } from 'react';

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

export function calculateLookFromInput(
  el: HTMLInputElement | null,
  preferCaret = false
): number {
  if (!el) return 0;
  const valLen = el.value ? el.value.length : 0;
  if (valLen === 0) return 0;

  const sel = typeof el.selectionStart === 'number' ? el.selectionStart : null;
  const charPos = sel !== null && (preferCaret || sel > 0) ? sel : valLen;
  const clampedChars = Math.min(32, Math.max(0, charPos));
  return clampLookValue((clampedChars / 32) * 100);
}

export const TeddyBearAvatar = forwardRef<TeddyBearHandle, TeddyBearAvatarProps>(
  ({ variant = 'login' }, ref) => {
    const [handsUp, setHandsUpState] = useState(false);
    const [lookX, setLookX] = useState(0); // -8 to 8
    const [lookY, setLookY] = useState(0); // -4 to 4
    const [expression, setExpression] = useState<'idle' | 'success' | 'fail'>('idle');

    useImperativeHandle(ref, () => ({
      lookAt: (normalizedLook: number) => {
        setHandsUpState(false);
        setExpression('idle');
        const clamped = clampLookValue(normalizedLook);
        // Map 0..100 to -6..6 horizontal shift
        setLookX((clamped / 50) * 6 - 6);
        setLookY(2);
      },
      setHandsUp: (up: boolean) => {
        setHandsUpState(up);
        if (up) {
          setLookX(0);
          setLookY(0);
        }
      },
      setIdle: () => {
        setHandsUpState(false);
        setLookX(0);
        setLookY(0);
        setExpression('idle');
      },
      triggerFail: () => {
        setHandsUpState(false);
        setExpression('fail');
        setLookX(0);
        setLookY(4);
      },
      triggerSuccess: () => {
        setHandsUpState(false);
        setExpression('success');
        setLookX(0);
        setLookY(0);
      },
      reset: () => {
        setHandsUpState(false);
        setLookX(0);
        setLookY(0);
        setExpression('idle');
      },
    }));

    const sizeClasses =
      variant === 'register' ? 'h-[142px] w-[169px]' : 'h-[162px] w-[192px]';

    return (
      <div
        aria-hidden="true"
        data-testid="teddy-bear-slot"
        data-variant={variant}
        className={`relative mx-auto flex items-center justify-center overflow-hidden pointer-events-none select-none ${sizeClasses}`}
      >
        <svg
          viewBox="0 0 160 140"
          className="w-full h-full drop-shadow-md transition-all duration-300"
          fill="none"
          xmlns="http://www.w3.org/2000/svg"
        >
          {/* Bear Ears */}
          <circle cx="42" cy="42" r="22" fill="#8B5A2B" />
          <circle cx="42" cy="42" r="13" fill="#D2996E" />
          <circle cx="118" cy="42" r="22" fill="#8B5A2B" />
          <circle cx="118" cy="42" r="13" fill="#D2996E" />

          {/* Bear Body / Shoulders */}
          <path
            d="M 35 140 C 35 105 125 105 125 140 Z"
            fill="#7A4D24"
          />

          {/* Bear Head */}
          <circle cx="80" cy="78" r="48" fill="#8B5A2B" />

          {/* Cheeks */}
          <circle cx="52" cy="90" r="9" fill="#E8A578" opacity="0.6" />
          <circle cx="108" cy="90" r="9" fill="#E8A578" opacity="0.6" />

          {/* Snout / Muzzle */}
          <ellipse cx="80" cy="92" rx="24" ry="18" fill="#D2996E" />

          {/* Nose */}
          <ellipse cx="80" cy="85" rx="9" ry="6" fill="#3D2314" />
          <circle cx="78" cy="84" r="2" fill="white" opacity="0.8" />

          {/* Mouth */}
          {expression === 'fail' ? (
            <path
              d="M 72 99 Q 80 94 88 99"
              stroke="#3D2314"
              strokeWidth="2.5"
              strokeLinecap="round"
            />
          ) : expression === 'success' ? (
            <path
              d="M 70 94 Q 80 106 90 94"
              stroke="#3D2314"
              strokeWidth="2.5"
              fill="#991B1B"
              strokeLinecap="round"
            />
          ) : (
            <path
              d="M 72 94 Q 80 102 88 94"
              stroke="#3D2314"
              strokeWidth="2.5"
              strokeLinecap="round"
            />
          )}

          {/* Eyes & Eyebrows */}
          {!handsUp && (
            <g className="transition-transform duration-150" style={{ transform: `translate(${lookX}px, ${lookY}px)` }}>
              {/* Left Eye */}
              <circle cx="62" cy="68" r="7.5" fill="#26170E" />
              <circle cx="60" cy="66" r="2.8" fill="white" />
              <circle cx="64" cy="70" r="1.2" fill="white" />

              {/* Right Eye */}
              {expression === 'success' ? (
                // Winking right eye on success
                <path d="M 92 68 Q 98 62 104 68" stroke="#26170E" strokeWidth="3" strokeLinecap="round" />
              ) : (
                <>
                  <circle cx="98" cy="68" r="7.5" fill="#26170E" />
                  <circle cx="96" cy="66" r="2.8" fill="white" />
                  <circle cx="100" cy="70" r="1.2" fill="white" />
                </>
              )}

              {/* Eyebrows */}
              {expression === 'fail' ? (
                <>
                  <line x1="56" y1="57" x2="68" y2="60" stroke="#4A2E1B" strokeWidth="2.5" strokeLinecap="round" />
                  <line x1="92" y1="60" x2="104" y2="57" stroke="#4A2E1B" strokeWidth="2.5" strokeLinecap="round" />
                </>
              ) : (
                <>
                  <line x1="56" y1="58" x2="68" y2="58" stroke="#4A2E1B" strokeWidth="2.5" strokeLinecap="round" />
                  <line x1="92" y1="58" x2="104" y2="58" stroke="#4A2E1B" strokeWidth="2.5" strokeLinecap="round" />
                </>
              )}
            </g>
          )}

          {/* Bear Paws / Hands */}
          {handsUp ? (
            /* Hands covering eyes when typing password */
            <g className="transition-all duration-300">
              {/* Left Paw Covering Left Eye */}
              <ellipse cx="62" cy="70" rx="18" ry="15" fill="#7A4D24" stroke="#5C3818" strokeWidth="2" />
              <circle cx="62" cy="72" r="7" fill="#D2996E" />
              <circle cx="53" cy="65" r="3" fill="#D2996E" />
              <circle cx="62" cy="60" r="3" fill="#D2996E" />
              <circle cx="71" cy="65" r="3" fill="#D2996E" />

              {/* Right Paw Covering Right Eye */}
              <ellipse cx="98" cy="70" rx="18" ry="15" fill="#7A4D24" stroke="#5C3818" strokeWidth="2" />
              <circle cx="98" cy="72" r="7" fill="#D2996E" />
              <circle cx="89" cy="65" r="3" fill="#D2996E" />
              <circle cx="98" cy="60" r="3" fill="#D2996E" />
              <circle cx="107" cy="65" r="3" fill="#D2996E" />
            </g>
          ) : (
            /* Resting Paws at bottom */
            <g className="transition-all duration-300">
              <ellipse cx="44" cy="128" rx="16" ry="12" fill="#7A4D24" />
              <ellipse cx="116" cy="128" rx="16" ry="12" fill="#7A4D24" />
            </g>
          )}
        </svg>
      </div>
    );
  }
);

TeddyBearAvatar.displayName = 'TeddyBearAvatar';
