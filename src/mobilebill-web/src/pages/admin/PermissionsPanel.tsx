import { Alert, Box, Button, ButtonBase, Checkbox, Chip, LinearProgress, Paper, Stack, Typography } from '@mui/material'
import { alpha, type Theme } from '@mui/material/styles'
import AccountBalanceIcon from '@mui/icons-material/AccountBalance'
import AddCircleOutlineIcon from '@mui/icons-material/AddCircleOutlineOutlined'
import AdminPanelSettingsIcon from '@mui/icons-material/AdminPanelSettings'
import AssessmentIcon from '@mui/icons-material/Assessment'
import BadgeIcon from '@mui/icons-material/Badge'
import BlockIcon from '@mui/icons-material/Block'
import CheckIcon from '@mui/icons-material/Check'
import DashboardIcon from '@mui/icons-material/Dashboard'
import EditOutlinedIcon from '@mui/icons-material/EditOutlined'
import EngineeringIcon from '@mui/icons-material/Engineering'
import GroupsIcon from '@mui/icons-material/Groups'
import HistoryIcon from '@mui/icons-material/History'
import LockIcon from '@mui/icons-material/Lock'
import ManageAccountsIcon from '@mui/icons-material/ManageAccounts'
import PaymentsIcon from '@mui/icons-material/Payments'
import ReceiptLongIcon from '@mui/icons-material/ReceiptLong'
import StorageIcon from '@mui/icons-material/Storage'
import VisibilityOutlinedIcon from '@mui/icons-material/VisibilityOutlined'
import { Fragment, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'

import { allRoles, roleLabels, type UserRole } from '../../api/authApi'
import { getPermissionMatrix, resetRolePermissions, saveRolePermissions, type ModulePermission, type PermissionAction, type PermissionMatrix, type RolePermissions } from '../../api/permissionsApi'
import { ConfirmActionDialog } from '../../components/common/ConfirmActionDialog'
import { ErrorState } from '../../components/common/ErrorState'
import { LoadingState } from '../../components/common/LoadingState'
import { brand, brandGradients } from '../../theme/theme'

const actions: PermissionAction[] = ['View', 'Create', 'Edit', 'Delete']
const field = { View: 'canView', Create: 'canCreate', Edit: 'canEdit', Delete: 'canDelete' } as const
type Draft = Record<string, ModulePermission>
type Palette = 'info' | 'success' | 'warning' | 'error'

const actionStyle: Record<PermissionAction, { label: string; icon: ReactNode; palette: Palette }> = {
  View: { label: 'View', icon: <VisibilityOutlinedIcon sx={{ fontSize: 16 }} />, palette: 'info' },
  Create: { label: 'Create', icon: <AddCircleOutlineIcon sx={{ fontSize: 16 }} />, palette: 'success' },
  Edit: { label: 'Edit', icon: <EditOutlinedIcon sx={{ fontSize: 16 }} />, palette: 'warning' },
  Delete: { label: 'Deactivate', icon: <BlockIcon sx={{ fontSize: 16 }} />, palette: 'error' },
}

const roleIcons: Record<UserRole, ReactNode> = {
  Administrator: <AdminPanelSettingsIcon />, ITEngineer: <EngineeringIcon />, HeadOfIt: <ManageAccountsIcon />, GroupHrManager: <GroupsIcon />,
  Cfo: <AccountBalanceIcon />, HrUser: <BadgeIcon />, FinanceUser: <PaymentsIcon />, Viewer: <VisibilityOutlinedIcon />,
}

const groupIcons: Record<string, ReactNode> = {
  General: <DashboardIcon fontSize="small" />, Billing: <ReceiptLongIcon fontSize="small" />, Masters: <StorageIcon fontSize="small" />,
  Reports: <AssessmentIcon fontSize="small" />, Audit: <HistoryIcon fontSize="small" />, Administration: <AdminPanelSettingsIcon fontSize="small" />,
}

const toDraft = (role: RolePermissions | undefined): Draft => Object.fromEntries((role?.permissions ?? []).map(item => [item.module, { ...item }]))
const summarise = (permissions: ModulePermission[]) => ({
  opened: permissions.filter(item => item.canView).length,
  changes: permissions.filter(item => item.canCreate || item.canEdit || item.canDelete).length,
  total: permissions.length,
})

// Which screens each role may open (View) and change (Create, Edit, Delete = Deactivate). Administrator always has
// everything. Billing preparation and the IT / HR / Finance approvals keep their fixed roles and are not set here.
export function PermissionsPanel() {
  const [matrix, setMatrix] = useState<PermissionMatrix>()
  const [error, setError] = useState<string>()
  const [notice, setNotice] = useState<string>()
  const [role, setRole] = useState<UserRole>('ITEngineer')
  // Unsaved changes for the role on screen; they belong to the saved permissions they started from.
  const [edits, setEdits] = useState<{ base: RolePermissions; values: Draft }>()
  const [busy, setBusy] = useState(false)
  const [pendingRole, setPendingRole] = useState<UserRole>()
  const [confirmReset, setConfirmReset] = useState(false)

  const current = matrix?.roles.find(item => item.role === role)
  const load = useCallback(async () => {
    setError(undefined)
    try { setMatrix(await getPermissionMatrix()) }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to load the permissions.') }
  }, [])
  useEffect(() => { void load() }, [load])
  const draft = useMemo(() => edits && edits.base === current ? edits.values : toDraft(current), [edits, current])

  const dirty = useMemo(() => Boolean(current) && current!.permissions.some(item => actions.some(action => draft[item.module]?.[field[action]] !== item[field[action]])), [current, draft])
  const groups = useMemo(() => {
    const result: Array<{ group: string; modules: PermissionMatrix['modules'] }> = []
    for (const module of matrix?.modules ?? []) {
      const existing = result.find(item => item.group === module.group)
      if (existing) existing.modules.push(module); else result.push({ group: module.group, modules: [module] })
    }
    return result
  }, [matrix])

  const chooseRole = (next: UserRole) => { if (next === role) return; if (dirty) setPendingRole(next); else { setRole(next); setNotice(undefined) } }

  const update = (changes: Draft) => { if (current) setEdits({ base: current, values: { ...draft, ...changes } }) }

  // Any change needs View; taking View away takes the changes away too.
  const toggle = (module: string, action: PermissionAction, checked: boolean) => {
    const item = { ...draft[module] }
    item[field[action]] = checked
    if (action === 'View' && !checked) { item.canCreate = false; item.canEdit = false; item.canDelete = false }
    if (action !== 'View' && checked) item.canView = true
    update({ [module]: item })
  }

  const setGroup = (modules: PermissionMatrix['modules'], level: 'full' | 'view' | 'none') => update(Object.fromEntries(modules.map(module => {
    const has = (action: PermissionAction) => module.actions.includes(action)
    return [module.key, { module: module.key, canView: level !== 'none', canCreate: level === 'full' && has('Create'), canEdit: level === 'full' && has('Edit'), canDelete: level === 'full' && has('Delete') }]
  })))

  const replaceRole = (saved: RolePermissions) => setMatrix(previous => previous && { ...previous, roles: previous.roles.map(item => item.role === saved.role ? saved : item) })

  const save = async () => {
    setBusy(true); setError(undefined); setNotice(undefined)
    try { replaceRole(await saveRolePermissions(role, Object.values(draft))); setNotice(`Permissions saved for ${roleLabels[role]}. Users with this role see the change the next time they sign in or reload the page.`) }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to save the permissions.') }
    finally { setBusy(false) }
  }

  const reset = async () => {
    setConfirmReset(false); setBusy(true); setError(undefined); setNotice(undefined)
    try { replaceRole(await resetRolePermissions(role)); setNotice(`${roleLabels[role]} is back to the default permissions.`) }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to reset the permissions.') }
    finally { setBusy(false) }
  }

  if (!matrix) return error ? <ErrorState message={error} onRetry={() => void load()} /> : <LoadingState label="Loading permissions…" />
  const locked = current?.locked ?? false
  const totals = summarise(Object.values(draft))

  return <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '280px minmax(0, 1fr)' }, gap: 3, alignItems: 'start' }}>
    {/* Roles */}
    <Paper variant="outlined" sx={{ p: 1.5, borderRadius: 3, position: { md: 'sticky' }, top: { md: 88 } }}>
      <Typography variant="overline" color="text.secondary" sx={{ px: 1 }}>Roles</Typography>
      <Stack role="radiogroup" aria-label="Role" spacing={0.75} sx={{ mt: 0.5, flexDirection: { xs: 'row', md: 'column' }, overflowX: { xs: 'auto', md: 'visible' }, gap: { xs: 1, md: 0 }, pb: { xs: 1, md: 0 } }}>
        {allRoles.map(option => {
          const info = matrix.roles.find(item => item.role === option.value)
          const counts = summarise(info?.permissions ?? [])
          const selected = option.value === role
          return <ButtonBase key={option.value} role="radio" aria-checked={selected} aria-label={option.label} onClick={() => chooseRole(option.value)}
            sx={theme => ({
              display: 'flex', alignItems: 'center', gap: 1.5, p: 1.25, borderRadius: 2.5, textAlign: 'left', justifyContent: 'flex-start', minWidth: { xs: 220, md: 0 }, flexShrink: 0,
              border: '1px solid', borderColor: selected ? alpha(brand.red, 0.45) : 'transparent',
              bgcolor: selected ? alpha(brand.red, theme.palette.mode === 'dark' ? 0.16 : 0.06) : 'transparent',
              boxShadow: selected ? `0 6px 18px ${alpha(brand.red, 0.15)}` : 'none',
              transition: 'background-color .15s ease, border-color .15s ease, box-shadow .15s ease',
              '&:hover': { bgcolor: selected ? undefined : 'action.hover' },
              '&.Mui-focusVisible': { outline: `2px solid ${brand.red}`, outlineOffset: 2 },
            })}>
            <Box sx={{ width: 38, height: 38, borderRadius: 2, display: 'grid', placeItems: 'center', color: '#fff', flexShrink: 0, backgroundImage: selected ? brandGradients.button : `linear-gradient(135deg, ${brand.indigo}, ${brand.purple})`, opacity: selected ? 1 : 0.85, '& svg': { fontSize: 20 } }}>
              {roleIcons[option.value]}
            </Box>
            <Box sx={{ flexGrow: 1, minWidth: 0 }}>
              <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center' }}>
                <Typography variant="body2" noWrap sx={{ fontWeight: 700 }}>{option.label}</Typography>
                {info?.locked && <LockIcon sx={{ fontSize: 14, color: 'text.secondary' }} />}
                {info?.customised && <Box component="span" title="Customised" sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: 'warning.main', flexShrink: 0 }} />}
              </Stack>
              <Typography variant="caption" color="text.secondary">{info?.locked ? 'Full access' : `${counts.opened} of ${counts.total} screens`}</Typography>
              <LinearProgress variant="determinate" value={counts.total ? (counts.opened / counts.total) * 100 : 0} aria-hidden
                sx={{ mt: 0.5, height: 4, borderRadius: 2, bgcolor: 'action.hover', '& .MuiLinearProgress-bar': { borderRadius: 2, backgroundImage: brandGradients.stripe } }} />
            </Box>
          </ButtonBase>
        })}
      </Stack>
    </Paper>

    <Stack spacing={2} sx={{ minWidth: 0 }}>
      {/* The chosen role */}
      <Paper variant="outlined" sx={{ borderRadius: 3, overflow: 'hidden' }}>
        <Box sx={{ height: 5, backgroundImage: brandGradients.stripe }} />
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ p: 2.5, alignItems: { sm: 'center' } }}>
          <Box sx={{ width: 56, height: 56, borderRadius: 3, display: 'grid', placeItems: 'center', color: '#fff', backgroundImage: brandGradients.topbar, boxShadow: `0 8px 20px ${alpha(brand.magenta, 0.3)}`, '& svg': { fontSize: 30 } }}>
            {roleIcons[role]}
          </Box>
          <Box sx={{ flexGrow: 1 }}>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap', rowGap: 0.5 }}>
              <Typography component="h3" variant="h6" sx={{ fontWeight: 800 }}>{roleLabels[role]}</Typography>
              {locked && <Chip size="small" icon={<LockIcon />} label="Locked" />}
              {current?.customised && <Chip size="small" color="warning" label="Customised" />}
              {!locked && current && !current.customised && <Chip size="small" variant="outlined" label="Default permissions" />}
            </Stack>
            <Stack direction="row" spacing={2.5} sx={{ mt: 0.75, flexWrap: 'wrap' }}>
              <Stat value={totals.opened} total={totals.total} label="screens opened" />
              <Stat value={totals.changes} label="screens with changes" />
            </Stack>
          </Box>
          {!locked && <Button color="warning" variant="outlined" disabled={busy || !current?.customised} onClick={() => setConfirmReset(true)}>Reset to Defaults</Button>}
        </Stack>
      </Paper>

      {locked && <Alert severity="info" icon={<AdminPanelSettingsIcon />}>The Administrator role always has full access to every screen, including User Management, and cannot be changed.</Alert>}
      <Alert severity="info" variant="outlined" sx={{ borderRadius: 2 }}>
        Deactivate keeps the record. Billing preparation, assessments, submitting and the IT / HR / Finance approvals follow the fixed approval chain.
        Only Administrators set permissions and manage Administrator accounts.
      </Alert>
      {error && <ErrorState message={error} />}
      {notice && <Alert severity="success">{notice}</Alert>}

      <Stack role="region" aria-label={`Permissions for ${roleLabels[role]}`} spacing={2}>
        {groups.map(({ group, modules }) => {
          const opened = modules.filter(module => draft[module.key]?.canView).length
          return <Paper key={group} variant="outlined" sx={{ borderRadius: 3, overflow: 'hidden' }}>
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={theme => ({ px: 2, py: 1.25, alignItems: { sm: 'center' }, bgcolor: alpha(theme.palette.primary.main, theme.palette.mode === 'dark' ? 0.12 : 0.04), borderBottom: '1px solid', borderColor: 'divider' })}>
              <Stack direction="row" spacing={1.25} sx={{ alignItems: 'center', flexGrow: 1 }}>
                <Box sx={{ width: 30, height: 30, borderRadius: 1.5, display: 'grid', placeItems: 'center', color: '#fff', backgroundImage: brandGradients.button }}>{groupIcons[group] ?? <DashboardIcon fontSize="small" />}</Box>
                <Typography component="h4" variant="subtitle1" sx={{ fontWeight: 800 }}>{group}</Typography>
                <Typography variant="caption" color="text.secondary">{opened} of {modules.length} opened</Typography>
              </Stack>
              {!locked && <Stack direction="row" spacing={0.5}>
                {([['full', 'Full access'], ['view', 'View only'], ['none', 'No access']] as const).map(([level, label]) =>
                  <Button key={level} size="small" disabled={busy} aria-label={`${label} for ${group}`} onClick={() => setGroup(modules, level)} sx={{ textTransform: 'none', fontWeight: 600 }}>{label}</Button>)}
              </Stack>}
            </Stack>
            {modules.map((module, index) => {
              const item = draft[module.key]
              return <Fragment key={module.key}>
                <Box role="group" aria-label={module.label} sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: 'minmax(0, 1fr) auto' }, gap: { xs: 1, lg: 2 }, alignItems: 'center', px: 2, py: 1.5, borderTop: index ? '1px solid' : 'none', borderColor: 'divider', '&:hover': { bgcolor: 'action.hover' } }}>
                  <Box sx={{ minWidth: 0 }}>
                    <Typography variant="body2" sx={{ fontWeight: 700 }}>{module.label}</Typography>
                    {module.note && <Typography variant="caption" color="text.secondary" sx={{ display: 'block', lineHeight: 1.4 }}>{module.note}</Typography>}
                  </Box>
                  <Stack direction="row" sx={{ flexWrap: 'wrap', gap: 0.75 }}>
                    {actions.map(action => module.actions.includes(action)
                      ? <ActionToggle key={action} action={action} checked={Boolean(item?.[field[action]])} disabled={locked || busy} label={`${action} ${module.label}`} onChange={checked => toggle(module.key, action, checked)} />
                      : <NotApplicable key={action} action={action} />)}
                  </Stack>
                </Box>
              </Fragment>
            })}
          </Paper>
        })}
      </Stack>

      {!locked && dirty && <Paper elevation={8} sx={{ position: 'sticky', bottom: 16, zIndex: 2, borderRadius: 3, overflow: 'hidden' }}>
        <Box sx={{ height: 4, backgroundImage: brandGradients.stripe }} />
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ px: 2, py: 1.5, alignItems: { sm: 'center' } }}>
          <Typography variant="body2" sx={{ flexGrow: 1, fontWeight: 600 }}>Unsaved changes for {roleLabels[role]}</Typography>
          <Stack direction="row" spacing={1}>
            <Button disabled={busy} onClick={() => setEdits(undefined)}>Discard</Button>
            <Button variant="contained" disabled={busy} onClick={() => void save()}>{busy ? 'Saving…' : 'Save Changes'}</Button>
          </Stack>
        </Stack>
      </Paper>}
    </Stack>

    <ConfirmActionDialog open={Boolean(pendingRole)} title="Discard unsaved changes?" message={`The changes to ${roleLabels[role]} have not been saved.`} confirmLabel="Discard" busy={false}
      onCancel={() => setPendingRole(undefined)} onConfirm={() => { if (pendingRole) { setRole(pendingRole); setNotice(undefined); setEdits(undefined) } setPendingRole(undefined) }} />
    <ConfirmActionDialog open={confirmReset} title="Reset to defaults" message={`${roleLabels[role]} will go back to the built-in permissions. Your changes for this role are removed.`} confirmLabel="Reset" busy={busy}
      onCancel={() => setConfirmReset(false)} onConfirm={() => void reset()} />
  </Box>
}

function Stat({ value, total, label }: { value: number; total?: number; label: string }) {
  return <Typography variant="body2" color="text.secondary">
    <Box component="span" sx={{ fontWeight: 800, fontSize: 18, color: 'text.primary', mr: 0.5 }}>{value}{total !== undefined && <Box component="span" sx={{ fontSize: 13, color: 'text.secondary', fontWeight: 600 }}> / {total}</Box>}</Box>
    {label}
  </Typography>
}

// A coloured on / off pill (a real checkbox underneath, for keyboards and screen readers).
function ActionToggle({ action, checked, disabled, label, onChange }: { action: PermissionAction; checked: boolean; disabled: boolean; label: string; onChange: (checked: boolean) => void }) {
  const style = actionStyle[action]
  const pill = (on: boolean) => <Box sx={(theme: Theme) => {
    const color = theme.palette[style.palette].main
    return {
      display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 0.5, width: 112, py: 0.6, borderRadius: 999, fontSize: 12.5, fontWeight: 700,
      border: '1.5px solid', borderColor: on ? color : 'divider', color: on ? color : 'text.secondary',
      bgcolor: on ? alpha(color, theme.palette.mode === 'dark' ? 0.2 : 0.1) : 'transparent',
      opacity: disabled ? 0.7 : 1, transition: 'all .15s ease',
    }
  }}>{on ? <CheckIcon sx={{ fontSize: 16 }} /> : style.icon}{style.label}</Box>
  return <Checkbox disableRipple checked={checked} disabled={disabled} onChange={event => onChange(event.target.checked)} icon={pill(false)} checkedIcon={pill(true)}
    slotProps={{ input: { 'aria-label': label } }}
    sx={{ p: 0, borderRadius: 999, '&:hover > div': { borderColor: disabled ? undefined : 'text.secondary' }, '&.Mui-focusVisible > div': { outline: `2px solid ${brand.red}`, outlineOffset: 2 } }} />
}

function NotApplicable({ action }: { action: PermissionAction }) {
  return <Box role="img" aria-label="Not applicable" title={`${actionStyle[action].label} does not apply to this screen`}
    sx={{ display: 'inline-flex', alignItems: 'center', justifyContent: 'center', width: 112, py: 0.6, borderRadius: 999, fontSize: 12.5, color: 'text.disabled', border: '1.5px dashed', borderColor: 'divider' }}>—</Box>
}
