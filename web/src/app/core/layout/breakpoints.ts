export interface BreakpointQueries {
  compact: string;
  medium: string;
  expanded: string;
}

/**
 * Media queries for the CDK BreakpointObserver, built from the custom properties styles.scss publishes from
 * src/styles/_breakpoints.scss, so CSS and TypeScript never disagree.
 */
export function breakpointQueries(doc: Document = document): BreakpointQueries {
  const style = getComputedStyle(doc.documentElement);
  const medium = style.getPropertyValue('--bp-medium-min').trim();
  const expanded = style.getPropertyValue('--bp-expanded-min').trim();
  if (!medium || !expanded) {
    throw new Error('Breakpoint custom properties are missing: is styles.scss loaded?');
  }

  return {
    compact: `(width < ${medium})`,
    medium: `(${medium} <= width < ${expanded})`,
    expanded: `(width >= ${expanded})`,
  };
}
