// How an employee appears in every drop-down: EPF — name with initials (calling name) — factory.
// The full name stands in when there are no initials; missing parts are left out.
export type EmployeeLabelParts = { epf?: unknown; nameWithInitials?: unknown; fullName?: unknown; callingName?: unknown; factory?: unknown }

const text = (value: unknown) => (value == null ? '' : String(value).trim())

export function employeeLabel({ epf, nameWithInitials, fullName, callingName, factory }: EmployeeLabelParts) {
  const name = text(nameWithInitials) || text(fullName)
  const calling = text(callingName)
  return [text(epf), calling ? `${name} (${calling})` : name, text(factory)].filter(Boolean).join(' — ')
}

// Extra words the drop-down search also matches (the full name), so a person can be found by any of their names.
export function employeeSearchText({ fullName }: EmployeeLabelParts) {
  return text(fullName)
}
