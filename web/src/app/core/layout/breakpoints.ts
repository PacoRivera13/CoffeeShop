export interface BreakpointQueries {
  compact: string;
  medium: string;
  expanded: string;
}

/**
 * Media queries for the CDK BreakpointObserver, built from the custom properties styles.scss publishes from
 * src/styles/_breakpoints.scss, so CSS and TypeScript never disagree. Uses min/max-width, like the Sass mixins,
 * because range syntax needs Safari 16.4.
 */
export function breakpointQueries(doc: Document = document): BreakpointQueries {
  const style = getComputedStyle(doc.documentElement);
  const medium = parsePixels(style.getPropertyValue('--bp-medium-min'));
  const expanded = parsePixels(style.getPropertyValue('--bp-expanded-min'));

  return {
    compact: `(max-width: ${medium - 0.02}px)`,
    medium: `(min-width: ${medium}px) and (max-width: ${expanded - 0.02}px)`,
    expanded: `(min-width: ${expanded}px)`,
  };
}

function parsePixels(value: string): number {
  const trimmed = value.trim();
  const pixels = Number.parseFloat(trimmed);
  if (!trimmed.endsWith('px') || Number.isNaN(pixels)) {
    throw new Error(`Breakpoint custom property '${trimmed}' is missing or not in px: is styles.scss loaded?`);
  }
  return pixels;
}
