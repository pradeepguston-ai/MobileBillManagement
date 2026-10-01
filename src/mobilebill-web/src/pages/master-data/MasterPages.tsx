import { MasterDataPage } from './MasterDataPage'
import { categoriesConfig, departmentsConfig, designationsConfig, employeesConfig, factoriesConfig, mobileAllocationsConfig, providersConfig } from './pageConfigs'

export const EmployeesPage = () => <MasterDataPage config={employeesConfig} />
export const MobileAllocationsPage = () => <MasterDataPage config={mobileAllocationsConfig} />

export const FactoriesPage = () => <MasterDataPage config={factoriesConfig} />
export const DepartmentsPage = () => <MasterDataPage config={departmentsConfig} />
export const DesignationsPage = () => <MasterDataPage config={designationsConfig} />
export const CategoriesPage = () => <MasterDataPage config={categoriesConfig} />
export const ProvidersPage = () => <MasterDataPage config={providersConfig} />
