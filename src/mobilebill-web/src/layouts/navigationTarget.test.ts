import { describe, expect, it } from 'vitest'

import { navigationTarget } from './navigationTarget'

describe('navigationTarget', () => {
  it.each([
    ['/monthly-bill-review', '/monthly-bill-review'],
    ['/billing/batch-1/review', '/monthly-bill-review'],
    ['/billing', '/billing'],
    ['/billing/new', '/billing'],
    ['/billing/batch-1/process', '/billing'],
    ['/billing/batch-1/lines', '/billing'],
    ['/billing/batch-1/exceptions', '/exception-review'],
    ['/exception-review', '/exception-review'],
    ['/employees', '/employees'],
    ['/', '/'],
  ])('%s highlights %s', (pathname, expected) => {
    expect(navigationTarget(pathname)).toBe(expected)
  })
})
