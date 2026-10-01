import {
  AppBar,
  Avatar,
  Box,
  Button,
  Drawer,
  IconButton,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  ListSubheader,
  Toolbar,
  Typography,
} from '@mui/material'
import { useMediaQuery, useTheme } from '@mui/material'
import ApartmentIcon from '@mui/icons-material/Apartment'
import BadgeIcon from '@mui/icons-material/Badge'
import CategoryIcon from '@mui/icons-material/Category'
import CellTowerIcon from '@mui/icons-material/CellTower'
import CloseIcon from '@mui/icons-material/Close'
import DashboardIcon from '@mui/icons-material/Dashboard'
import FactoryIcon from '@mui/icons-material/Factory'
import HowToRegIcon from '@mui/icons-material/HowToReg'
import LogoutIcon from '@mui/icons-material/Logout'
import MenuIcon from '@mui/icons-material/Menu'
import PeopleAltIcon from '@mui/icons-material/PeopleAlt'
import ReceiptLongIcon from '@mui/icons-material/ReceiptLong'
import ReportProblemIcon from '@mui/icons-material/ReportProblem'
import SmartphoneIcon from '@mui/icons-material/Smartphone'
import SummarizeIcon from '@mui/icons-material/Summarize'
import { type ReactNode, useState } from 'react'
import { Link as RouterLink, Outlet, useLocation, useNavigate } from 'react-router-dom'

import { roleLabels } from '../api/authApi'
import { useAuth } from '../auth/AuthContext'

const drawerWidth = 264

const iconByPath: Record<string, ReactNode> = {
  '/': <DashboardIcon fontSize="small" />,
  '/billing': <ReceiptLongIcon fontSize="small" />,
  '/exception-review': <ReportProblemIcon fontSize="small" />,
  '/employees': <PeopleAltIcon fontSize="small" />,
  '/mobile-allocations': <SmartphoneIcon fontSize="small" />,
  '/factories': <FactoryIcon fontSize="small" />,
  '/departments': <ApartmentIcon fontSize="small" />,
  '/designations': <BadgeIcon fontSize="small" />,
  '/categories': <CategoryIcon fontSize="small" />,
  '/providers': <CellTowerIcon fontSize="small" />,
  '/reports/monthly-bill': <SummarizeIcon fontSize="small" />,
  '/admin/users': <HowToRegIcon fontSize="small" />,
}

export function AppLayout() {
  const theme = useTheme()
  const isCompact = useMediaQuery(theme.breakpoints.down('md'))
  const [mobileOpen, setMobileOpen] = useState(false)
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const signOut = () => { logout(); navigate('/login', { replace: true }) }
  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <AppBar position="fixed" sx={{ zIndex: (theme) => theme.zIndex.drawer + 1 }}>
        <Toolbar>
          {isCompact && <IconButton color="inherit" aria-label="Open navigation" onClick={() => setMobileOpen(true)} sx={{ mr: 1 }}><MenuIcon /></IconButton>}
          <Typography component="div" variant="h6" sx={{ flexGrow: 1 }}>
            Mobile Bill Management
          </Typography>
          {user && <Box sx={{ display: { xs: 'none', sm: 'flex' }, alignItems: 'center', gap: 1, mr: 2 }}>
            <Avatar sx={{ width: 28, height: 28, fontSize: 14, bgcolor: 'primary.dark' }}>{initials(user.displayName)}</Avatar>
            <Typography variant="body2">{user.displayName} · {roleLabels[user.role]}</Typography>
          </Box>}
          <Button color="inherit" onClick={signOut} startIcon={<LogoutIcon fontSize="small" />}>Sign out</Button>
        </Toolbar>
      </AppBar>
      {!isCompact && <Drawer
        variant="permanent"
        sx={{
          width: drawerWidth,
          flexShrink: 0,
          '& .MuiDrawer-paper': { boxSizing: 'border-box', width: drawerWidth, borderRight: '1px solid', borderColor: 'divider' },
        }}
      >
        <Toolbar />
        <NavigationContent isAdministrator={user?.role === 'Administrator'} />
      </Drawer>}
      {isCompact && <Drawer variant="temporary" open={mobileOpen} onClose={() => setMobileOpen(false)} slotProps={{ paper: { sx: { width: drawerWidth } } }}><Toolbar sx={{ justifyContent: 'flex-end' }}><IconButton aria-label="Close navigation" onClick={() => setMobileOpen(false)}><CloseIcon /></IconButton></Toolbar><NavigationContent isAdministrator={user?.role === 'Administrator'} onNavigate={() => setMobileOpen(false)} /></Drawer>}
      <Box component="main" sx={{ flexGrow: 1, minWidth: 0, p: { xs: 2, sm: 3, md: 4 } }}>
        <Toolbar />
        <Outlet />
      </Box>
    </Box>
  )
}

function initials(name: string) {
  const parts = name.trim().split(/\s+/)
  return ((parts[0]?.[0] ?? '') + (parts.length > 1 ? parts[parts.length - 1][0] : '')).toUpperCase()
}

function NavigationContent({ isAdministrator, onNavigate }: { isAdministrator?: boolean; onNavigate?: () => void }) {
  return <List aria-label="Primary navigation" sx={{ px: 1 }}>
    <NavigationLink to="/" label="Dashboard" onNavigate={onNavigate} />
    <NavigationGroup label="Billing" links={[["/billing", "Billing Batches"], ["/exception-review", "Exception Review"], ["/billing", "Bill Review"]]} onNavigate={onNavigate} />
    <NavigationGroup label="Masters" links={[["/employees", "Employees"], ["/mobile-allocations", "Mobile Allocations"], ["/factories", "Factories"], ["/departments", "Departments"], ["/designations", "Designations"], ["/categories", "Categories"], ["/providers", "Telecom Providers"]]} onNavigate={onNavigate} />
    <NavigationGroup label="Reports" links={[["/reports/monthly-bill", "Monthly Bill Report"]]} onNavigate={onNavigate} />
    {isAdministrator && <NavigationGroup label="Administration" links={[["/admin/users", "Pending Users"]]} onNavigate={onNavigate} />}
  </List>
}

function NavigationGroup({ label, links, onNavigate }: { label: string; links: string[][]; onNavigate?: () => void }) {
  return <>
    <ListSubheader disableSticky sx={{ lineHeight: 2.5, mt: 1 }}>{label}</ListSubheader>
    {links.map(([to, linkLabel]) => <NavigationLink key={`${label}-${linkLabel}`} to={to} label={linkLabel} onNavigate={onNavigate} />)}
  </>
}

function NavigationLink({ to, label, onNavigate }: { to: string; label: string; onNavigate?: () => void }) {
  const location = useLocation()
  const selected = location.pathname === to
  return <ListItemButton component={RouterLink} to={to} selected={selected} onClick={onNavigate} sx={{ mb: 0.25 }}>
    <ListItemIcon sx={{ minWidth: 36 }}>{iconByPath[to] ?? <DashboardIcon fontSize="small" />}</ListItemIcon>
    <ListItemText primary={label} />
  </ListItemButton>
}
