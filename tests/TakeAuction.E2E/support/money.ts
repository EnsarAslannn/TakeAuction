/**
 * The UI renders money through Intl with tr-TR grouping. Matching on the grouped digits
 * alone keeps assertions readable and immune to how the runtime spaces the currency sign.
 */
const grouped = new Intl.NumberFormat("tr-TR", { maximumFractionDigits: 0 });

export function groupedAmount(value: number): string {
  return grouped.format(value);
}

export function amountPattern(value: number): RegExp {
  const digits = groupedAmount(value).replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

  return new RegExp(`(^|\\D)${digits}(\\D|$)`);
}
