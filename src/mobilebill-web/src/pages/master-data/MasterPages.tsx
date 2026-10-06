import { MasterDataPage } from './MasterDataPage'
import { categoriesConfig, designationsConfig, employeesConfig, factoriesConfig, providersConfig } from './pageConfigs'

export { DepartmentsPage } from './DepartmentsPage'
export { MobileAllocationsPage } from './MobileAllocationsPage'

export const EmployeesPage = () => <MasterDataPage config={employeesConfig} />

export const FactoriesPage = () => <MasterDataPage config={factoriesConfig} />
export const DesignationsPage = () => <MasterDataPage config={designationsConfig} />
export const CategoriesPage = () => <MasterDataPage config={categoriesConfig} />
export const ProvidersPage = () => <MasterDataPage config={providersConfig} />
