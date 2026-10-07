import { BrowserRouter, Route, Routes } from 'react-router-dom'

import { RequireAuth } from './auth/RequireAuth'
import { AppLayout } from './layouts/AppLayout'
import { DashboardPage } from './pages/DashboardPage'
import { MonthlyBillReportPage } from './pages/MonthlyBillReportPage'
import { VasReportPage } from './pages/VasReportPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { CategoriesPage, DepartmentsPage, DesignationsPage, EmployeesPage, FactoriesPage, MobileAllocationsPage, MobileDevicesPage, MobilePackagesPage, ProvidersPage } from './pages/master-data/MasterPages'
import { ExceptionReviewPage } from './pages/ExceptionReviewPage'
import { MonthlyBillReviewPage } from './pages/MonthlyBillReviewPage'
import { BillingBatchListPage } from './pages/billing/BillingBatchListPage'
import { MonthlyBillOverviewPage } from './pages/billing/MonthlyBillOverviewPage'
import { BillingProcessingPage } from './pages/billing/BillingProcessingPage'
import { BillLinesPage } from './pages/billing/BillLinesPage'
import { CreateBillingBatchPage } from './pages/billing/CreateBillingBatchPage'
import { LoginPage } from './pages/auth/LoginPage'
import { RegisterPage } from './pages/auth/RegisterPage'
import { PendingApprovalPage } from './pages/auth/PendingApprovalPage'
import { UserManagementPage } from './pages/admin/UserManagementPage'
import { PasswordResetRequestsPage } from './pages/admin/PasswordResetRequestsPage'
import { ForgotPasswordPage } from './pages/auth/ForgotPasswordPage'

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="login" element={<LoginPage />} />
        <Route path="register" element={<RegisterPage />} />
        <Route path="forgot-password" element={<ForgotPasswordPage />} />
        <Route path="pending-approval" element={<PendingApprovalPage />} />
        <Route element={<RequireAuth><AppLayout /></RequireAuth>}>
          <Route index element={<DashboardPage />} />
          <Route path="employees" element={<EmployeesPage />} />
          <Route path="mobile-allocations" element={<MobileAllocationsPage />} />
          <Route path="devices" element={<MobileDevicesPage />} />

          <Route path="factories" element={<FactoriesPage />} />
          <Route path="departments" element={<DepartmentsPage />} />          <Route path="designations" element={<DesignationsPage />} />
          <Route path="categories" element={<CategoriesPage />} />
          <Route path="providers" element={<ProvidersPage />} />
          <Route path="packages" element={<MobilePackagesPage />} />
          <Route path="billing" element={<BillingBatchListPage />} />
          <Route path="monthly-bill-review" element={<MonthlyBillOverviewPage />} />
          <Route path="billing/new" element={<CreateBillingBatchPage />} />
          <Route path="billing/:batchId/process" element={<BillingProcessingPage />} />
          <Route path="billing/:batchId/lines" element={<BillLinesPage />} />
          <Route path="exception-review" element={<ExceptionReviewPage />} />
          <Route path="billing/:batchId/exceptions" element={<ExceptionReviewPage />} />
          <Route path="billing/:batchId/review" element={<MonthlyBillReviewPage />} />
          <Route path="reports/monthly-bill" element={<MonthlyBillReportPage />} />
          <Route path="reports/vas" element={<VasReportPage />} />
          <Route path="admin/users" element={<RequireAuth roles={['Administrator']}><UserManagementPage /></RequireAuth>} />
          <Route path="admin/password-resets" element={<RequireAuth roles={['Administrator']}><PasswordResetRequestsPage /></RequireAuth>} />
        </Route>
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
