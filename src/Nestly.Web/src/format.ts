const money = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  maximumFractionDigits: 0,
});

const count = new Intl.NumberFormat('en-US');

export const formatMoney = (value: number): string => money.format(value);

export const formatCount = (value: number): string => count.format(value);

/** The count with its noun agreeing, so a single result does not read "1 listings". */
export function formatListings(total: number): string {
  return `${formatCount(total)} ${total === 1 ? 'listing' : 'listings'}`;
}

export function formatBedrooms(bedrooms: number): string {
  if (bedrooms === 0) {
    return 'Studio';
  }

  return bedrooms === 1 ? '1 bedroom' : `${String(bedrooms)} bedrooms`;
}

export function formatBathrooms(bathrooms: number): string {
  // Half baths are real in this dataset, so 1.5 must not round to 2.
  const value = Number.isInteger(bathrooms) ? String(bathrooms) : bathrooms.toFixed(1);

  return `${value} ${bathrooms === 1 ? 'bath' : 'baths'}`;
}

/** Cuts at the last word boundary before `max`, so a card never ends mid-word. */
export function truncate(text: string, max: number): string {
  if (text.length <= max) {
    return text;
  }

  const cut = text.slice(0, max);
  const boundary = cut.lastIndexOf(' ');

  return `${boundary > max * 0.6 ? cut.slice(0, boundary) : cut}…`;
}
