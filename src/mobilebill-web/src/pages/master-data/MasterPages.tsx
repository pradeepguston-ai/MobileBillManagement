import { MasterDataPage } from './MasterDataPage'
import { categoriesConfig, designationsConfig, factoriesConfig, mobilePackagesConfig, providersConfig } from './pageConfigs'

export { DepartmentsPage } from './DepartmentsPage'
export { EmployeesPage } from './EmployeesPage'
export { MobileAllocationsPage } from './MobileAllocationsPage'
export { MobileDevicesPage } from './MobileDevicesPage'

export const FactoriesPage = () => <MasterDataPage config={factoriesConfig} />
export const DesignationsPage = () => <MasterDataPage config={designationsConfig} />
export const CategoriesPage = () => <MasterDataPage config={categoriesConfig} />
export const ProvidersPage = () => <MasterDataPage config={providersConfig} />
export const MobilePackagesPage = () => <MasterDataPage config={mobilePackagesConfig} />
