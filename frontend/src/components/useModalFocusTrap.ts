import { useEffect, useLayoutEffect, useRef, RefObject } from 'react';

const FOCUSABLE_SELECTOR = [
  'button:not([disabled])',
  'a[href]',
  'input:not([disabled]):not([type="hidden"])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"]):not([disabled])',
].join(', ');

// Stack of active modal entries so nested modals (e.g. PinModal over BottomSheet) coordinate focus cleanly
interface ModalStackEntry {
  id: symbol;
  getContainer: () => HTMLDivElement | null;
}

const activeModalStack: ModalStackEntry[] = [];

// Track the last interactive element outside dialogs in case pointer/touch events do not focus before onClick
let lastExternalTrigger: HTMLElement | null = null;

if (typeof document !== 'undefined') {
  const captureTrigger = (e: Event) => {
    const target = e.target as HTMLElement | null;
    if (!target || typeof target.closest !== 'function') return;
    if (target.closest('[role="dialog"]')) return;
    const focusable = target.closest<HTMLElement>(FOCUSABLE_SELECTOR);
    if (focusable) {
      lastExternalTrigger = focusable;
    }
  };

  document.addEventListener('pointerdown', captureTrigger, true);
  document.addEventListener('mousedown', captureTrigger, true);
  document.addEventListener('click', captureTrigger, true);
  document.addEventListener(
    'keydown',
    (e: KeyboardEvent) => {
      if (e.key === 'Enter' || e.key === ' ') {
        captureTrigger(e);
      }
    },
    true
  );
}

export function getFocusableElements(container: HTMLElement | null): HTMLElement[] {
  if (!container) return [];
  const nodes = Array.from(container.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR));
  return nodes.filter((el) => {
    if (el.getAttribute('aria-hidden') === 'true') return false;
    if (el.closest('[inert]')) return false;
    const style = window.getComputedStyle(el);
    if (style.display === 'none' || style.visibility === 'hidden') return false;
    return el.offsetParent !== null || el.getClientRects().length > 0;
  });
}

function canReceiveFocus(el: HTMLElement | null, allowInsideDialog = false): el is HTMLElement {
  if (!el || el === document.body || el === document.documentElement) return false;
  if (!document.contains(el)) return false;
  if (el.hasAttribute('disabled') || el.getAttribute('aria-disabled') === 'true') return false;
  if (!allowInsideDialog && el.closest('[role="dialog"]')) return false;
  const style = window.getComputedStyle(el);
  if (style.display === 'none' || style.visibility === 'hidden') return false;
  return el.offsetParent !== null || el.getClientRects().length > 0;
}

export function clearInertOnAncestors(el: HTMLElement | null) {
  let curr: HTMLElement | null = el;
  while (curr && curr !== document.body) {
    if (curr.hasAttribute('inert')) {
      curr.removeAttribute('inert');
    }
    if (curr.getAttribute('aria-hidden') === 'true') {
      curr.removeAttribute('aria-hidden');
    }
    curr = curr.parentElement;
  }
}

function findFallbackFocusTarget(): HTMLElement | null {
  const mainScroll = document.getElementById('sw-main-scroll');
  if (mainScroll) {
    clearInertOnAncestors(mainScroll);
    mainScroll.removeAttribute('inert');
    mainScroll.removeAttribute('aria-hidden');
    const candidates = getFocusableElements(mainScroll);
    if (candidates.length > 0) {
      return candidates[0];
    }
  }
  const phoneShell = document.getElementById('sw-phone-shell');
  if (phoneShell) {
    const candidates = getFocusableElements(phoneShell);
    if (candidates.length > 0) {
      return candidates[0];
    }
  }
  return null;
}

export function focusDialogContainer(
  container: HTMLElement | null,
  preferredTarget?: HTMLElement | null
) {
  if (!container) return;
  // Never allow an ancestor of the active modal to retain aria-hidden or inert
  clearInertOnAncestors(container);

  const activeBefore = document.activeElement as HTMLElement | null;
  if (activeBefore && activeBefore !== document.body && !container.contains(activeBefore)) {
    activeBefore.blur();
  }

  if (
    preferredTarget &&
    container.contains(preferredTarget) &&
    !preferredTarget.hasAttribute('disabled')
  ) {
    preferredTarget.focus({ preventScroll: true });
    if (document.activeElement === preferredTarget) return;
  }

  const focusables = getFocusableElements(container);
  if (focusables.length > 0) {
    focusables[0].focus({ preventScroll: true });
    if (container.contains(document.activeElement)) return;
  }

  container.focus({ preventScroll: true });
}

interface UseModalFocusTrapOptions {
  isOpen: boolean;
  onClose: () => void;
  allowEscape?: boolean;
  initialFocusRef?: RefObject<HTMLElement | null>;
  returnFocusRef?: RefObject<HTMLElement | null>;
}

export function useModalFocusTrap({
  isOpen,
  onClose,
  allowEscape = true,
  initialFocusRef,
  returnFocusRef,
}: UseModalFocusTrapOptions) {
  const dialogRef = useRef<HTMLDivElement>(null);
  const triggerElementRef = useRef<HTMLElement | null>(null);
  const onCloseRef = useRef(onClose);
  const allowEscapeRef = useRef(allowEscape);
  const initialFocusRefLatest = useRef(initialFocusRef);
  const returnFocusRefLatest = useRef(returnFocusRef);

  useEffect(() => {
    onCloseRef.current = onClose;
    allowEscapeRef.current = allowEscape;
    initialFocusRefLatest.current = initialFocusRef;
    returnFocusRefLatest.current = returnFocusRef;
  }, [onClose, allowEscape, initialFocusRef, returnFocusRef]);

  useLayoutEffect(() => {
    if (!isOpen) return;

    const modalSymbol = Symbol('sw-modal');
    const entry: ModalStackEntry = {
      id: modalSymbol,
      getContainer: () => dialogRef.current,
    };
    activeModalStack.push(entry);

    // Record the element that opened this modal before moving focus inside
    const explicitTrigger = returnFocusRefLatest.current?.current || null;
    const activeEl = document.activeElement as HTMLElement | null;
    const validActive =
      activeEl &&
      activeEl !== document.body &&
      activeEl !== document.documentElement &&
      (!dialogRef.current || !dialogRef.current.contains(activeEl))
        ? activeEl
        : null;

    triggerElementRef.current = explicitTrigger || validActive || lastExternalTrigger;

    // Move focus into the modal synchronously BEFORE background regions receive aria-hidden="true"
    focusDialogContainer(dialogRef.current, initialFocusRefLatest.current?.current || null);

    // Mark any underlying modals in activeModalStack inert while this top modal is open
    for (let i = 0; i < activeModalStack.length - 1; i++) {
      const lowerContainer = activeModalStack[i].getContainer();
      if (
        lowerContainer &&
        (!dialogRef.current || !lowerContainer.contains(dialogRef.current))
      ) {
        lowerContainer.setAttribute('aria-hidden', 'true');
        lowerContainer.setAttribute('inert', '');
      }
    }

    // Reinforce focus on next frame in case portal children finish layout
    const rafId = window.requestAnimationFrame(() => {
      if (activeModalStack[activeModalStack.length - 1]?.id !== modalSymbol) return;
      const container = dialogRef.current;
      if (!container) return;
      if (!container.contains(document.activeElement)) {
        focusDialogContainer(container, initialFocusRefLatest.current?.current || null);
      }
    });

    const handleKeyDown = (e: KeyboardEvent) => {
      if (activeModalStack[activeModalStack.length - 1]?.id !== modalSymbol) return;
      const container = dialogRef.current;
      if (!container) return;

      if (e.key === 'Escape' && allowEscapeRef.current) {
        e.preventDefault();
        e.stopPropagation();
        onCloseRef.current();
        return;
      }

      if (e.key === 'Tab') {
        const focusables = getFocusableElements(container);
        if (focusables.length === 0) {
          e.preventDefault();
          container.focus({ preventScroll: true });
          return;
        }

        const first = focusables[0];
        const last = focusables[focusables.length - 1];
        const current = document.activeElement as HTMLElement | null;

        if (e.shiftKey) {
          if (!current || !container.contains(current) || current === container || current === first) {
            e.preventDefault();
            last.focus({ preventScroll: true });
          }
        } else {
          if (!current || !container.contains(current) || current === container || current === last) {
            e.preventDefault();
            first.focus({ preventScroll: true });
          }
        }
      }
    };

    const handleFocusIn = (e: FocusEvent) => {
      if (activeModalStack[activeModalStack.length - 1]?.id !== modalSymbol) return;
      const container = dialogRef.current;
      if (!container) return;

      const target = e.target as Node | null;
      if (target && !container.contains(target)) {
        focusDialogContainer(container, initialFocusRefLatest.current?.current || null);
      }
    };

    document.addEventListener('keydown', handleKeyDown, true);
    document.addEventListener('focusin', handleFocusIn, true);

    return () => {
      window.cancelAnimationFrame(rafId);
      document.removeEventListener('keydown', handleKeyDown, true);
      document.removeEventListener('focusin', handleFocusIn, true);

      const exitingOverlay = dialogRef.current?.closest('[data-sw-sheet-open]');
      if (exitingOverlay) {
        exitingOverlay.removeAttribute('data-sw-sheet-open');
      }

      // Blur focused element inside the closing modal before it unmounts
      const currentActive = document.activeElement as HTMLElement | null;
      if (currentActive && dialogRef.current && dialogRef.current.contains(currentActive)) {
        currentActive.blur();
      }

      const idx = activeModalStack.findIndex((item) => item.id === modalSymbol);
      if (idx !== -1) {
        activeModalStack.splice(idx, 1);
      }

      // If another modal is still open underneath (e.g. BottomSheet under GlassSelect sheet or PinModal), return focus to it
      if (activeModalStack.length > 0) {
        const topModal = activeModalStack[activeModalStack.length - 1];
        const parentContainer = topModal.getContainer();
        if (parentContainer) {
          const savedTrigger =
            returnFocusRefLatest.current?.current || triggerElementRef.current;
          const restoreToParent = () => {
            parentContainer.removeAttribute('inert');
            parentContainer.removeAttribute('aria-hidden');
            clearInertOnAncestors(parentContainer);
            if (
              savedTrigger &&
              parentContainer.contains(savedTrigger) &&
              canReceiveFocus(savedTrigger, true)
            ) {
              savedTrigger.focus({ preventScroll: true });
            } else {
              focusDialogContainer(parentContainer);
            }
          };
          restoreToParent();
          window.requestAnimationFrame(restoreToParent);
        }
        return;
      }

      // Otherwise restore focus to the background trigger element after clearing aria-hidden/inert
      const savedTrigger =
        returnFocusRefLatest.current?.current ||
        triggerElementRef.current ||
        lastExternalTrigger;

      const attemptRestore = () => {
        if (activeModalStack.length > 0) return;
        clearInertOnAncestors(savedTrigger);
        const mainScroll = document.getElementById('sw-main-scroll');
        if (mainScroll) {
          mainScroll.removeAttribute('inert');
          mainScroll.removeAttribute('aria-hidden');
        }

        if (canReceiveFocus(savedTrigger, false)) {
          savedTrigger.focus({ preventScroll: true });
          return;
        }

        const fallback = findFallbackFocusTarget();
        if (fallback) {
          fallback.focus({ preventScroll: true });
        }
      };

      attemptRestore();
      window.requestAnimationFrame(attemptRestore);
    };
  }, [isOpen]);

  return { dialogRef };
}

