import { computed, DOCUMENT, effect, inject, Injectable, signal } from '@angular/core';

export type ThemeMode = 'system' | 'light' | 'dark';
export type ColorScheme = 'light' | 'dark';

export const THEME_STORAGE_KEY = 'roastery.theme';

/** Follows the system color scheme until the user picks one; the choice survives reloads. */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly doc = inject(DOCUMENT);
  private readonly media = this.doc.defaultView?.matchMedia('(prefers-color-scheme: dark)');
  private readonly systemDark = signal(this.media?.matches ?? false);

  readonly mode = signal<ThemeMode>(this.readStoredMode());
  readonly scheme = computed<ColorScheme>(() => {
    const mode = this.mode();
    return mode === 'system' ? (this.systemDark() ? 'dark' : 'light') : mode;
  });

  constructor() {
    this.media?.addEventListener('change', (event) => this.systemDark.set(event.matches));

    effect(() => {
      const mode = this.mode();
      const root = this.doc.documentElement;
      if (mode === 'system') root.removeAttribute('data-theme');
      else root.setAttribute('data-theme', mode);
    });
  }

  /** Switches to the opposite of the scheme on screen and remembers the choice. */
  toggle(): void {
    const next: ColorScheme = this.scheme() === 'dark' ? 'light' : 'dark';
    this.mode.set(next);
    try {
      this.doc.defaultView?.localStorage.setItem(THEME_STORAGE_KEY, next);
    } catch {
      // Storage can be unavailable (private mode, blocked site data): the choice then lasts for this visit only.
    }
  }

  private readStoredMode(): ThemeMode {
    try {
      const stored = this.doc.defaultView?.localStorage.getItem(THEME_STORAGE_KEY);
      return stored === 'light' || stored === 'dark' ? stored : 'system';
    } catch {
      return 'system';
    }
  }
}
