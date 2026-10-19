// Sound service for SpendWise interactive UI feedback
// Implements Web Audio API with smooth envelopes, customizable pitch, and localStorage preference.

class SoundService {
  private ctx: AudioContext | null = null;
  private enabled: boolean = false;
  private currentGainNodes: GainNode[] = [];

  constructor() {
    // Load sound preference from localStorage, default to false (OFF) as specified
    if (typeof window !== 'undefined') {
      const stored = localStorage.getItem('spendwise_haptic_sound_enabled');
      this.enabled = stored === 'true';
    }
  }

  public isEnabled(): boolean {
    return this.enabled;
  }

  public setEnabled(val: boolean): void {
    this.enabled = val;
    if (typeof window !== 'undefined') {
      localStorage.setItem('spendwise_haptic_sound_enabled', val ? 'true' : 'false');
      // Dispatch storage/custom event so all UI components update synchronously
      window.dispatchEvent(new CustomEvent('spendwise_sound_preference_changed', { detail: val }));
    }
    if (!val) {
      // Immediate silence: fade and cut any active trailing nodes
      this.stopAll();
    }
  }

  private initContext(): AudioContext | null {
    if (typeof window === 'undefined') return null;
    if (!this.ctx) {
      const AudioCtx = window.AudioContext || (window as any).webkitAudioContext;
      if (AudioCtx) {
        this.ctx = new AudioCtx();
      }
    }
    if (this.ctx && this.ctx.state === 'suspended') {
      this.ctx.resume().catch(() => {});
    }
    return this.ctx;
  }

  private stopAll(): void {
    if (this.currentGainNodes.length > 0) {
      const now = this.ctx?.currentTime || 0;
      this.currentGainNodes.forEach((g) => {
        try {
          g.gain.cancelScheduledValues(now);
          g.gain.setValueAtTime(0, now);
        } catch {
          // ignore
        }
      });
      this.currentGainNodes = [];
    }
  }

  /**
   * Play an elegant tab switch sound
   * Frequency reference: 523, 587, 659, 698, 784 Hz (C5, D5, E5, F5, G5)
   */
  public playTabSound(freq: number = 659): void {
    if (!this.enabled) return;

    try {
      const ctx = this.initContext();
      if (!ctx) return;

      const now = ctx.currentTime;

      // Limit concurrent sound overlaps to avoid cacophony
      if (this.currentGainNodes.length >= 2) {
        const oldest = this.currentGainNodes.shift();
        if (oldest) {
          try {
            oldest.gain.cancelScheduledValues(now);
            oldest.gain.setTargetAtTime(0, now, 0.02);
          } catch {}
        }
      }

      // Master gain node for envelope
      const masterGain = ctx.createGain();
      masterGain.connect(ctx.destination);
      this.currentGainNodes.push(masterGain);

      // Main Oscillator: Sine wave for pure, soft, non-piercing sound
      const osc = ctx.createOscillator();
      osc.type = 'sine';
      osc.frequency.setValueAtTime(freq, now);

      // Subtle Harmonic Oscillator: Triangle wave at octave with very low gain for touch of clarity
      const harmOsc = ctx.createOscillator();
      const harmGain = ctx.createGain();
      harmOsc.type = 'triangle';
      harmOsc.frequency.setValueAtTime(freq * 1.5, now);
      harmGain.gain.setValueAtTime(0.15, now);
      harmOsc.connect(harmGain);
      harmGain.connect(masterGain);

      // Envelope: Attack 5ms, Decay & Release ~280ms
      const attack = 0.005;
      const decay = 0.28;
      const peakVolume = 0.035; // Gentle 3.5% gain

      masterGain.gain.setValueAtTime(0.0001, now);
      masterGain.gain.exponentialRampToValueAtTime(peakVolume, now + attack);
      masterGain.gain.exponentialRampToValueAtTime(0.0001, now + attack + decay);

      osc.connect(masterGain);

      osc.start(now);
      harmOsc.start(now);

      const stopTime = now + attack + decay + 0.05;
      osc.stop(stopTime);
      harmOsc.stop(stopTime);

      setTimeout(() => {
        const idx = this.currentGainNodes.indexOf(masterGain);
        if (idx !== -1) {
          this.currentGainNodes.splice(idx, 1);
        }
      }, (attack + decay + 0.1) * 1000);
    } catch {
      // Audio playback errors must never disrupt navigation
    }
  }
}

export const soundService = new SoundService();
