import { describe, expect, it } from 'vitest'

import { generateOneTimePassword } from './oneTimePassword'

describe('generateOneTimePassword', () => {
  it('mixes upper case, lower case, digits and symbols without look-alike characters', () => {
    for (let run = 0; run < 200; run++) {
      const password = generateOneTimePassword()
      expect(password).toHaveLength(12)
      expect(password).toMatch(/[A-Z]/)
      expect(password).toMatch(/[a-z]/)
      expect(password).toMatch(/[2-9]/)
      expect(password).toMatch(/[@#$%&*?!]/)
      expect(password).not.toMatch(/[0O1lI]/)
    }
  })

  it('gives a different password each time and honours a longer length', () => {
    const passwords = new Set(Array.from({ length: 50 }, () => generateOneTimePassword()))
    expect(passwords.size).toBe(50)
    expect(generateOneTimePassword(20)).toHaveLength(20)
  })
})
