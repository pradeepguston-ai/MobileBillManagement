// Characters that are easy to read out or type from a message: no 0/O, 1/l/I.
const upper = 'ABCDEFGHJKLMNPQRSTUVWXYZ'
const lower = 'abcdefghijkmnopqrstuvwxyz'
const digits = '23456789'
const symbols = '@#$%&*?!'
const all = upper + lower + digits + symbols

function pick(set: string) {
  // Rejection sampling, so every character is equally likely.
  const limit = 256 - (256 % set.length)
  const buffer = new Uint8Array(1)
  do crypto.getRandomValues(buffer)
  while (buffer[0] >= limit)
  return set[buffer[0] % set.length]
}

// A random password for a new account, with at least one upper-case letter, lower-case letter, digit and symbol.
export function generateOneTimePassword(length = 12) {
  const characters = [pick(upper), pick(lower), pick(digits), pick(symbols)]
  while (characters.length < length) characters.push(pick(all))
  for (let index = characters.length - 1; index > 0; index--) {
    const swap = Math.floor((crypto.getRandomValues(new Uint32Array(1))[0] / 2 ** 32) * (index + 1))
    ;[characters[index], characters[swap]] = [characters[swap], characters[index]]
  }
  return characters.join('')
}
