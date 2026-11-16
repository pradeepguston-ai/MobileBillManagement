import { BrowserRouter, Route, Routes } from 'react-router-dom'

import { RequireAuth } from './auth/RequireAuth'
import { RequirePermission } from './auth/RequirePermission'
import { userManagementPage } from './auth/permissions'
import { AppLayout } from './layouts/AppLayout'
import { DashboardPage } from './pages/DashboardPage'
import { MonthlyBillReportPage } from './pages/MonthlyBillReportPage'
import { VasReportPage } from './pages/VasReportPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { CategoriesPage, DepartmentsPage, DesignationsPage, EmployeesPage, FactoriesPage, MobileAllocationsPage, MobileDevicesPage, MobilePackagesPage, ProvidersPage, ThirdPartyCompaniesPage } from './pages/master-data/MasterPages'
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
import { ReportDistributionPage } from './pages/admin/ReportDistributionPage'
import { EmailHistoryPage } from './pages/admin/EmailHistoryPage'
import { ActivityLogPage } from './pages/ActivityLogPage'
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
          <Route index element={<RequirePermission module="dashboard"><DashboardPage /></RequirePermission>} />
          <Route path="employees" element={<RequirePermission module="employees"><EmployeesPage /></RequirePermission>} />
          <Route path="mobile-allocations" element={<RequirePermission module="mobile-allocations"><MobileAllocationsPage /></RequirePermission>} />
          <Route path="devices" element={<RequirePermission module="mobile-devices"><MobileDevicesPage /></RequirePermission>} />

          <Route path="factories" element={<RequirePermission module="factories"><FactoriesPage /></RequirePermission>} />
          <Route path="departments" element={<RequirePermission module="departments"><DepartmentsPage /></RequirePermission>} />
          <Route path="designations" element={<RequirePermission module="designations"><DesignationsPage /></RequirePermission>} />
          <Route path="categories" element={<RequirePermission module="categories"><CategoriesPage /></RequirePermission>} />
          <Route path="providers" element={<RequirePermission module="providers"><ProvidersPage /></RequirePermission>} />
          <Route path="third-party-companies" element={<RequirePermission module="third-party-companies"><ThirdPartyCompaniesPage /></RequirePermission>} />
          <Route path="packages" element={<RequirePermission module="mobile-packages"><MobilePackagesPage /></RequirePermission>} />
          <Route path="billing" element={<RequirePermission module="billing-batches"><BillingBatchListPage /></RequirePermission>} />
          <Route path="monthly-bill-review" element={<RequirePermission module="monthly-bill-review"><MonthlyBillOverviewPage /></RequirePermission>} />
          <Route path="billing/new" element={<RequirePermission module="billing-batches"><CreateBillingBatchPage /></RequirePermission>} />
          <Route path="billing/:batchId/process" element={<RequirePermission module="billing-batches"><BillingProcessingPage /></RequirePermission>} />
          <Route path="billing/:batchId/lines" element={<RequirePermission module="billing-batches"><BillLinesPage /></RequirePermission>} />
          <Route path="exception-review" element={<RequirePermission module="exception-review"><ExceptionReviewPage /></RequirePermission>} />
          <Route path="billing/:batchId/exceptions" element={<RequirePermission module="exception-review"><ExceptionReviewPage /></RequirePermission>} />
          <Route path="billing/:batchId/review" element={<RequirePermission module="monthly-bill-review"><MonthlyBillReviewPage /></RequirePermission>} />
          <Route path="reports/monthly-bill" element={<RequirePermission module="monthly-bill-report"><MonthlyBillReportPage /></RequirePermission>} />
          <Route path="reports/vas" element={<RequirePermission module="vas-report"><VasReportPage /></RequirePermission>} />
          <Route path="activity-log" element={<RequirePermission module="activity-log"><ActivityLogPage /></RequirePermission>} />
          <Route path="admin/users" element={<RequirePermission module={userManagementPage}><UserManagementPage /></RequirePermission>} />
          <Route path="admin/password-resets" element={<RequirePermission module="user-management"><PasswordResetRequestsPage /></RequirePermission>} />
          <Route path="admin/report-distribution" element={<RequirePermission module="report-distribution"><ReportDistributionPage /></RequirePermission>} />
          <Route path="admin/email-history" element={<RequirePermission module="report-distribution"><EmailHistoryPage /></RequirePermission>} />
        </Route>
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
