// Which sidebar item a page belongs to, so batch sub-pages keep their section highlighted:
// a batch's review page counts as Monthly Bill Review, its exceptions page as Exception Review,
// and its other pages (new, process, extracted lines) as Billing Batches.
export function navigationTarget(pathname: string) {
  if (/^\/billing\/[^/]+\/review\/?$/.test(pathname)) return '/monthly-bill-review'
  if (/^\/billing\/[^/]+\/exceptions\/?$/.test(pathname)) return '/exception-review'
  if (pathname === '/billing' || pathname.startsWith('/billing/')) return '/billing'
  return pathname
}
