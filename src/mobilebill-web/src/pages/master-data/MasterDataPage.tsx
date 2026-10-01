import { Alert, Box, Pagination, Stack } from '@mui/material'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { createMasterData, deactivateMasterData, listAllMasterData, listMasterData, updateMasterData } from '../../api/masterDataApi'
import type { EntityRow } from '../../api/types'
import { DeactivateConfirmationDialog } from '../../components/master-data/DeactivateConfirmationDialog'
import { ErrorState } from '../../components/master-data/ErrorState'
import { LoadingState } from '../../components/master-data/LoadingState'
import { MasterDataDialog, type FormField, type FormFieldOption } from '../../components/master-data/MasterDataDialog'
import { MasterDataTable, type Column } from '../../components/master-data/MasterDataTable'
import { MasterDataToolbar } from '../../components/master-data/MasterDataToolbar'
import { EmptyState } from '../../components/common/EmptyState'
import { PageHeader } from '../../components/common/PageHeader'

export type MasterDataLookup = { fieldKey: string; path: string; toOption: (row: EntityRow) => FormFieldOption }
export type MasterDataPageConfig<T extends EntityRow> = { title: string; path: string; columns: Column<T>[]; fields: FormField[]; lookups?: MasterDataLookup[]; toForm: (row?: T) => Record<string, string>; toRequest: (values: Record<string, string>) => unknown }
export function MasterDataPage<T extends EntityRow>({ config }: { config: MasterDataPageConfig<T> }) {
  const [search, setSearch] = useState(''); const [isActive, setIsActive] = useState(''); const [page, setPage] = useState(1); const [data, setData] = useState<{ items: T[]; totalPages: number }>(); const [error, setError] = useState<string>(); const [loading, setLoading] = useState(true); const [editing, setEditing] = useState<T>(); const [values, setValues] = useState<Record<string, string>>({}); const [deactivating, setDeactivating] = useState<T>(); const [validation, setValidation] = useState<Record<string, string>>({}); const [lookupOptions, setLookupOptions] = useState<Record<string, FormFieldOption[]>>({});
  const activeValue = useMemo(() => isActive === '' ? undefined : isActive === 'true', [isActive])
  const load = useCallback(async () => { setLoading(true); setError(undefined); try { const response = await listMasterData<T>(config.path, { pageNumber: page, pageSize: 20, search, isActive: activeValue }); setData(response) } catch (exception) { setError(exception instanceof Error ? exception.message : 'Unable to load data.') } finally { setLoading(false) } }, [activeValue, config.path, page, search])
  useEffect(() => { const timer = window.setTimeout(() => { void load() }, 0); return () => window.clearTimeout(timer) }, [load])
  useEffect(() => {
    if (!config.lookups?.length) return
    let cancelled = false
    Promise.all(config.lookups.map(async lookup => {
      const items = await listAllMasterData<EntityRow>(lookup.path, '', true)
      return [lookup.fieldKey, items.map(lookup.toOption)] as const
    })).then(entries => { if (!cancelled) setLookupOptions(Object.fromEntries(entries)) }).catch(exception => { if (!cancelled) setError(exception instanceof Error ? exception.message : 'Unable to load lookup data.') })
    return () => { cancelled = true }
  }, [config.lookups])
  const dialogFields = useMemo(() => config.fields.map(field => {
    const options = field.type === 'select' ? lookupOptions[field.key] ?? [] : field.options
    const filteredOptions = field.dependsOn ? options?.filter(option => option.parentValue === values[field.dependsOn!]) : options
    return { ...field, options: filteredOptions, disabled: field.disabled || Boolean(field.dependsOn && !values[field.dependsOn]) }
  }), [config.fields, lookupOptions, values])
  const openCreate = () => { setEditing(undefined); setValues(config.toForm()); setValidation({}) }
  const openEdit = (row: T) => { setEditing(row); setValues(config.toForm(row)); setValidation({}) }
  const changeValue = (key: string, value: string) => setValues(current => {
    const next = { ...current, [key]: value }
    config.fields.filter(field => field.dependsOn === key).forEach(field => {
      const selectedOption = lookupOptions[field.key]?.find(option => option.value === current[field.key])
      if (selectedOption?.parentValue !== value) next[field.key] = ''
    })
    return next
  })
  const save = async () => { const requiredErrors = Object.fromEntries(config.fields.filter(field => field.required && !values[field.key]?.trim()).map(field => [field.key, `${field.label} is required.`])); if (Object.keys(requiredErrors).length) { setValidation(requiredErrors); return } try { if (editing) await updateMasterData<T>(config.path, editing.id, config.toRequest(values)); else await createMasterData<T>(config.path, config.toRequest(values)); setEditing(undefined); setValues({}); await load() } catch (exception) { setValidation({ form: exception instanceof Error ? exception.message : 'Unable to save.' }) } }
  const deactivate = async () => { if (!deactivating) return; try { await deactivateMasterData(config.path, deactivating.id); setDeactivating(undefined); await load() } catch (exception) { setError(exception instanceof Error ? exception.message : 'Unable to deactivate.') } }
  return <Stack spacing={3}><PageHeader title={config.title} /><MasterDataToolbar search={search} onSearchChange={value => { setSearch(value); setPage(1) }} isActive={isActive} onIsActiveChange={value => { setIsActive(value); setPage(1) }} onCreate={openCreate} />{error && <ErrorState message={error} />}{loading ? <LoadingState /> : (data?.items.length ?? 0) === 0 ? <EmptyState message={`No ${config.title.toLowerCase()} match the selected filters.`} /> : <Box sx={{ overflowX: 'auto' }}><MasterDataTable columns={config.columns} rows={data?.items ?? []} onEdit={openEdit} onDeactivate={setDeactivating} /></Box>}{(data?.totalPages ?? 0) > 0 && <Pagination count={data?.totalPages ?? 0} page={page} onChange={(_, value) => setPage(value)} />}<MasterDataDialog open={Boolean(editing) || Object.keys(values).length > 0} title={editing ? `Edit ${config.title}` : `Create ${config.title}`} fields={dialogFields} values={values} errors={validation} onChange={changeValue} onCancel={() => { setEditing(undefined); setValues({}) }} onSave={() => void save()} />{validation.form && <Alert severity="error">{validation.form}</Alert>}<DeactivateConfirmationDialog open={Boolean(deactivating)} title={config.title} onCancel={() => setDeactivating(undefined)} onConfirm={() => void deactivate()} /></Stack>
}
