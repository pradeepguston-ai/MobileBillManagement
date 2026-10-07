import { Box, Button, Pagination, Stack } from '@mui/material'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { createMasterData, deactivateMasterData, listAllMasterData, listMasterData, updateMasterData } from '../../api/masterDataApi'
import type { EntityRow } from '../../api/types'
import { DeactivateConfirmationDialog } from '../../components/master-data/DeactivateConfirmationDialog'
import { ErrorState } from '../../components/master-data/ErrorState'
import { LoadingState } from '../../components/master-data/LoadingState'
import { MasterDataDialog, type FormField, type FormFieldOption } from '../../components/master-data/MasterDataDialog'
import { MasterDataTable, type Column } from '../../components/master-data/MasterDataTable'
import { MasterDataToolbar } from '../../components/master-data/MasterDataToolbar'
import { ImportDialog } from '../../components/master-data/ImportDialog'
import { useCanEditMasterData } from '../../auth/AuthContext'
import type { UserRole } from '../../api/authApi'
import { EmptyState } from '../../components/common/EmptyState'
import { PageHeader } from '../../components/common/PageHeader'

export type MasterDataLookup = { fieldKey: string; path: string; toOption: (row: EntityRow) => FormFieldOption }
// A narrower edit for roles without full edit rights, such as moving a mobile number to another employee:
// the edit dialog opens with only editableFields changeable, and save sends just those values.
export type MasterDataLimitedEdit<T extends EntityRow> = { roles: readonly UserRole[]; label: string; title: string; editableFields: string[]; canEditRow?: (row: T) => boolean; save: (id: string, values: Record<string, string>) => Promise<unknown> }
// An extra per-row action with its own small form, such as Release to Pool. fields and submit receive whether the
// user has full master-data rights, so an action can keep some fields (like amounts) for Administrator and IT Engineer.
export type MasterDataRowAction<T extends EntityRow> = { label: string; color?: 'primary' | 'warning' | 'error'; show: (row: T) => boolean; title: (row: T) => string; fields: (fullEditor: boolean) => FormField[]; initialValues: (row: T) => Record<string, string>; submit: (row: T, values: Record<string, string>, fullEditor: boolean) => Promise<unknown> }
// Values to fill in when a select field changes, for example a package's rental; creating is false when editing a row.
export type MasterDataAutofill = (key: string, option: FormFieldOption | undefined, creating: boolean) => Record<string, string> | undefined
// editRoles: who may create, edit and deactivate (default: Administrator and IT Engineer).
// rowActionRoles: who sees rowActions. importer: who may bulk upload from Excel (POST {path}/import).
// autofill also runs in row-action dialogs, as creating. createDefaults: starting values for the Create dialog, from the loaded select options.
// noDeactivate hides the Deactivate button for records that leave use another way (for example a device marked Lost or Retired).
export type MasterDataPageConfig<T extends EntityRow> = { title: string; path: string; columns: Column<T>[]; fields: FormField[]; lookups?: MasterDataLookup[]; toForm: (row?: T) => Record<string, string>; toRequest: (values: Record<string, string>) => unknown; editRoles?: readonly UserRole[]; limitedEdit?: MasterDataLimitedEdit<T>; rowActions?: MasterDataRowAction<T>[]; rowActionRoles?: readonly UserRole[]; importer?: { roles: readonly UserRole[]; title: string }; autofill?: MasterDataAutofill; createDefaults?: (options: Record<string, FormFieldOption[]>) => Record<string, string>; noDeactivate?: boolean }
// hideHeader is for a page shown inside a tab, where the surrounding screen already has the heading.
// reloadKey: change it to make the page reload its rows (for example after another tab changed them).
export function MasterDataPage<T extends EntityRow>({ config, hideHeader = false, reloadKey = 0 }: { config: MasterDataPageConfig<T>; hideHeader?: boolean; reloadKey?: number }) {
  const canEdit = useCanEditMasterData(config.editRoles)
  const fullEditor = useCanEditMasterData()
  const canLimitedEdit = useCanEditMasterData(config.limitedEdit?.roles ?? []) && !canEdit && Boolean(config.limitedEdit)
  const canRowActions = useCanEditMasterData(config.rowActionRoles ?? []) && Boolean(config.rowActions?.length)
  const canImport = useCanEditMasterData(config.importer?.roles ?? []) && Boolean(config.importer)
  const [importOpen, setImportOpen] = useState(false)
  const [limited, setLimited] = useState(false)
  const [running, setRunning] = useState<{ action: MasterDataRowAction<T>; row: T; values: Record<string, string>; errors: Record<string, string> }>()
  const [search, setSearch] = useState(''); const [isActive, setIsActive] = useState(''); const [page, setPage] = useState(1); const [data, setData] = useState<{ items: T[]; totalPages: number }>(); const [error, setError] = useState<string>(); const [loading, setLoading] = useState(true); const [editing, setEditing] = useState<T>(); const [values, setValues] = useState<Record<string, string>>({}); const [deactivating, setDeactivating] = useState<T>(); const [validation, setValidation] = useState<Record<string, string>>({}); const [lookupOptions, setLookupOptions] = useState<Record<string, FormFieldOption[]>>({});
  const activeValue = useMemo(() => isActive === '' ? undefined : isActive === 'true', [isActive])
  const load = useCallback(async () => { setLoading(true); setError(undefined); try { const response = await listMasterData<T>(config.path, { pageNumber: page, pageSize: 20, search, isActive: activeValue }); setData(response) } catch (exception) { setError(exception instanceof Error ? exception.message : 'Unable to load data.') } finally { setLoading(false) } }, [activeValue, config.path, page, search])
  useEffect(() => { const timer = window.setTimeout(() => { void load() }, 0); return () => window.clearTimeout(timer) }, [load, reloadKey])
  // Reloaded after every save or row action, since those can change the select lists (for example devices In Stock).
  const [lookupKey, setLookupKey] = useState(0)
  useEffect(() => {
    if (!config.lookups?.length) return
    let cancelled = false
    Promise.all(config.lookups.map(async lookup => {
      const items = await listAllMasterData<EntityRow>(lookup.path, '', true)
      return [lookup.fieldKey, items.map(lookup.toOption)] as const
    })).then(entries => { if (!cancelled) setLookupOptions(Object.fromEntries(entries)) }).catch(exception => { if (!cancelled) setError(exception instanceof Error ? exception.message : 'Unable to load lookup data.') })
    return () => { cancelled = true }
  }, [config.lookups, lookupKey])
  const dialogFields = useMemo(() => config.fields.map(field => {
    const options = field.type === 'select' ? lookupOptions[field.key] ?? field.options ?? [] : field.options
    const filteredOptions = field.dependsOn ? options?.filter(option => option.parentValue === values[field.dependsOn!]) : options
    const lockedByLimitedEdit = limited && !config.limitedEdit?.editableFields.includes(field.key)
    return { ...field, options: filteredOptions, disabled: field.disabled || lockedByLimitedEdit || Boolean(field.dependsOn && !values[field.dependsOn]) }
  }), [config.fields, config.limitedEdit, limited, lookupOptions, values])
  const openCreate = () => { setLimited(false); setEditing(undefined); setValues({ ...config.toForm(), ...config.createDefaults?.(lookupOptions) }); setValidation({}) }
  const openEdit = (row: T) => { setLimited(false); setEditing(row); setValues(config.toForm(row)); setValidation({}) }
  const openLimitedEdit = (row: T) => { setLimited(true); setEditing(row); setValues(config.toForm(row)); setValidation({}) }
  const filled = (key: string, value: string, creating: boolean) => config.autofill?.(key, lookupOptions[key]?.find(option => option.value === value), creating) ?? {}
  const changeValue = (key: string, value: string) => setValues(current => {
    const next = { ...current, [key]: value, ...filled(key, value, !editing) }
    // Clears dependent selections that no longer belong to their parent, down the chain (Department → Section → Sub Section).
    const clearDependents = (parentKey: string) => config.fields.filter(field => field.dependsOn === parentKey).forEach(field => {
      const selectedOption = lookupOptions[field.key]?.find(option => option.value === next[field.key])
      if (next[field.key] && selectedOption?.parentValue !== next[parentKey]) { next[field.key] = ''; clearDependents(field.key) }
    })
    clearDependents(key)
    return next
  })
  const save = async () => { const requiredErrors = Object.fromEntries(config.fields.filter(field => field.required && !values[field.key]?.trim()).map(field => [field.key, `${field.label} is required.`])); if (Object.keys(requiredErrors).length) { setValidation(requiredErrors); return } try { if (editing && limited && config.limitedEdit) await config.limitedEdit.save(editing.id, values); else if (editing) await updateMasterData<T>(config.path, editing.id, config.toRequest(values)); else await createMasterData<T>(config.path, config.toRequest(values)); setEditing(undefined); setValues({}); setLimited(false); setLookupKey(key => key + 1); await load() } catch (exception) { setValidation({ form: exception instanceof Error ? exception.message : 'Unable to save.' }) } }
  const actionFields = useMemo(() => running ? running.action.fields(fullEditor).map(field => field.type === 'select' ? { ...field, options: lookupOptions[field.key] ?? field.options ?? [] } : field) : [], [fullEditor, lookupOptions, running])
  const startAction = (action: MasterDataRowAction<T>, row: T) => setRunning({ action, row, values: action.initialValues(row), errors: {} })
  const runAction = async () => {
    if (!running) return
    const missing = Object.fromEntries(actionFields.filter(field => field.required && !running.values[field.key]?.trim()).map(field => [field.key, `${field.label} is required.`]))
    if (Object.keys(missing).length) { setRunning({ ...running, errors: missing }); return }
    try { await running.action.submit(running.row, running.values, fullEditor); setRunning(undefined); setLookupKey(key => key + 1); await load() }
    catch (exception) { setRunning(current => current && { ...current, errors: { form: exception instanceof Error ? exception.message : 'Unable to complete the action.' } }) }
  }
  const rowActions = canRowActions ? (row: T) => config.rowActions!.filter(action => action.show(row)).map(action => <Button key={action.label} size="small" color={action.color ?? 'primary'} onClick={() => startAction(action, row)}>{action.label}</Button>) : undefined
  const deactivate = async () => { if (!deactivating) return; try { await deactivateMasterData(config.path, deactivating.id); setDeactivating(undefined); await load() } catch (exception) { setError(exception instanceof Error ? exception.message : 'Unable to deactivate.') } }
  return <Stack spacing={3}>{!hideHeader && <PageHeader title={config.title} />}<MasterDataToolbar search={search} onSearchChange={value => { setSearch(value); setPage(1) }} isActive={isActive} onIsActiveChange={value => { setIsActive(value); setPage(1) }} onCreate={canEdit ? openCreate : undefined} onImport={canImport ? () => setImportOpen(true) : undefined} />{canImport && config.importer && <ImportDialog open={importOpen} title={config.importer.title} path={config.path} onClose={() => setImportOpen(false)} onImported={() => void load()} />}{error && <ErrorState message={error} />}{loading ? <LoadingState /> : (data?.items.length ?? 0) === 0 ? <EmptyState message={`No ${config.title.toLowerCase()} match the selected filters.`} /> : <Box sx={{ overflowX: 'auto' }}><MasterDataTable columns={config.columns} rows={data?.items ?? []} onEdit={canEdit ? openEdit : canLimitedEdit ? openLimitedEdit : undefined} editLabel={canLimitedEdit ? config.limitedEdit?.label : undefined} canEditRow={canLimitedEdit ? config.limitedEdit?.canEditRow : undefined} onDeactivate={canEdit && !config.noDeactivate ? setDeactivating : undefined} extraActions={rowActions} /></Box>}{(data?.totalPages ?? 0) > 0 && <Pagination count={data?.totalPages ?? 0} page={page} onChange={(_, value) => setPage(value)} />}<MasterDataDialog open={Boolean(editing) || Object.keys(values).length > 0} title={editing ? (limited && config.limitedEdit ? config.limitedEdit.title : `Edit ${config.title}`) : `Create ${config.title}`} fields={dialogFields} values={values} errors={validation} onChange={changeValue} onCancel={() => { setEditing(undefined); setValues({}); setLimited(false) }} onSave={() => void save()} />{running && <MasterDataDialog open title={running.action.title(running.row)} fields={actionFields} values={running.values} errors={running.errors} onChange={(key, value) => setRunning(current => current && { ...current, values: { ...current.values, [key]: value, ...(actionFields.find(field => field.key === key)?.disabled ? {} : filled(key, value, true)) } })} onCancel={() => setRunning(undefined)} onSave={() => void runAction()} saveLabel={running.action.label} />}<DeactivateConfirmationDialog open={Boolean(deactivating)} title={config.title} onCancel={() => setDeactivating(undefined)} onConfirm={() => void deactivate()} /></Stack>
}
