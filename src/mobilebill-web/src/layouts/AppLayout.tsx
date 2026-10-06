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
  Tooltip,
  Typography,
} from '@mui/material'
import { useMediaQuery, useTheme } from '@mui/material'
import { useColorScheme } from '@mui/material/styles'
import DarkModeIcon from '@mui/icons-material/DarkMode'
import LightModeIcon from '@mui/icons-material/LightMode'
import ApartmentIcon from '@mui/icons-material/Apartment'
import BadgeIcon from '@mui/icons-material/Badge'
import CategoryIcon from '@mui/icons-material/Category'
import CellTowerIcon from '@mui/icons-material/CellTower'
import CloseIcon from '@mui/icons-material/Close'
import DashboardIcon from '@mui/icons-material/Dashboard'
import FactCheckIcon from '@mui/icons-material/FactCheck'
import FactoryIcon from '@mui/icons-material/Factory'
import ManageAccountsIcon from '@mui/icons-material/ManageAccounts'
import LockResetIcon from '@mui/icons-material/LockReset'
import LogoutIcon from '@mui/icons-material/Logout'
import MenuIcon from '@mui/icons-material/Menu'
import PeopleAltIcon from '@mui/icons-material/PeopleAlt'
import ReceiptLongIcon from '@mui/icons-material/ReceiptLong'
import ReportProblemIcon from '@mui/icons-material/ReportProblem'
import SmartphoneIcon from '@mui/icons-material/Smartphone'
import SummarizeIcon from '@mui/icons-material/Summarize'
import AppsIcon from '@mui/icons-material/Apps'
import { type ReactNode, useState } from 'react'
import { Link as RouterLink, Outlet, useLocation, useNavigate } from 'react-router-dom'

import { roleLabels } from '../api/authApi'
import { useAuth } from '../auth/AuthContext'
import { navigationTarget } from './navigationTarget'
import gustonLogo from '../assets/guston-logo.webp'
import { brand, brandGradients } from '../theme/theme'

const drawerWidth = 264

const iconByPath: Record<string, ReactNode> = {
  '/': <DashboardIcon fontSize="small" />,
  '/billing': <ReceiptLongIcon fontSize="small" />,
  '/monthly-bill-review': <FactCheckIcon fontSize="small" />,
  '/exception-review': <ReportProblemIcon fontSize="small" />,
  '/employees': <PeopleAltIcon fontSize="small" />,
  '/mobile-allocations': <SmartphoneIcon fontSize="small" />,
  '/factories': <FactoryIcon fontSize="small" />,
  '/departments': <ApartmentIcon fontSize="small" />,
  '/designations': <BadgeIcon fontSize="small" />,
  '/categories': <CategoryIcon fontSize="small" />,
  '/providers': <CellTowerIcon fontSize="small" />,
  '/reports/monthly-bill': <SummarizeIcon fontSize="small" />,
  '/reports/vas': <AppsIcon fontSize="small" />,
  '/admin/users': <ManageAccountsIcon fontSize="small" />,
  '/admin/password-resets': <LockResetIcon fontSize="small" />,
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
          <Box sx={{ flexGrow: 1, display: 'flex', alignItems: 'center', gap: 1.5 }}>
            <Box component="img" src={gustonLogo} alt="Guston Ltd" sx={{ height: 40, width: 'auto', filter: 'brightness(0) invert(1)' }} />
            <Typography component="div" variant="h6">Mobile Bill Management</Typography>
          </Box>
          {user && <Box sx={{ display: { xs: 'none', sm: 'flex' }, alignItems: 'center', gap: 1, mr: 2 }}>
            <Avatar sx={{ width: 28, height: 28, fontSize: 14, bgcolor: 'rgba(255,255,255,0.22)', border: '1px solid rgba(255,255,255,0.3)' }}>{initials(user.displayName)}</Avatar>
            <Typography variant="body2">{user.displayName} · {roleLabels[user.role]}</Typography>
          </Box>}
          <ColorModeToggle />
          <Button color="inherit" onClick={signOut} startIcon={<LogoutIcon fontSize="small" />}>Sign out</Button>
        </Toolbar>
      </AppBar>
      {!isCompact && <Drawer
        variant="permanent"
        sx={{
          width: drawerWidth,
          flexShrink: 0,
          '& .MuiDrawer-paper': { ...sidebarPaperSx, width: drawerWidth },
        }}
      >
        <Toolbar />
        <NavigationContent isAdministrator={user?.role === 'Administrator'} />
      </Drawer>}
      {isCompact && <Drawer variant="temporary" open={mobileOpen} onClose={() => setMobileOpen(false)} slotProps={{ paper: { sx: { ...sidebarPaperSx, width: drawerWidth } } }}><Toolbar sx={{ justifyContent: 'flex-end' }}><IconButton aria-label="Close navigation" onClick={() => setMobileOpen(false)} sx={{ color: '#fff' }}><CloseIcon /></IconButton></Toolbar><NavigationContent isAdministrator={user?.role === 'Administrator'} onNavigate={() => setMobileOpen(false)} /></Drawer>}
      <Box component="main" sx={{ flexGrow: 1, minWidth: 0, p: { xs: 2, sm: 3, md: 4 } }}>
        <Toolbar />
        <Outlet />
      </Box>
    </Box>
  )
}

function ColorModeToggle() {
  const { mode, systemMode, setMode } = useColorScheme()
  if (!mode) return null
  const dark = (mode === 'system' ? systemMode : mode) === 'dark'
  return <Tooltip title={dark ? 'Switch to light mode' : 'Switch to dark mode'}>
    <IconButton color="inherit" aria-label="Toggle dark mode" onClick={() => setMode(dark ? 'light' : 'dark')} sx={{ mr: 1 }}>
      {dark ? <LightModeIcon fontSize="small" /> : <DarkModeIcon fontSize="small" />}
    </IconButton>
  </Tooltip>
}

const sidebarPaperSx = {
  boxSizing: 'border-box',
  backgroundImage: brandGradients.sidebar,
  backgroundColor: '#7C1830',
  color: '#fff',
  borderRight: '1px solid rgba(55,20,65,0.34)',
  boxShadow: '8px 0 26px rgba(81,31,72,0.18)',
  overflowX: 'hidden',
  '&::before': { content: '""', display: 'block', height: 5, flexShrink: 0, backgroundImage: brandGradients.stripe },
} as const

function initials(name: string) {
  const parts = name.trim().split(/\s+/)
  return ((parts[0]?.[0] ?? '') + (parts.length > 1 ? parts[parts.length - 1][0] : '')).toUpperCase()
}

function NavigationContent({ isAdministrator, onNavigate }: { isAdministrator?: boolean; onNavigate?: () => void }) {
  return <Box sx={{ display: 'flex', flexDirection: 'column', flexGrow: 1 }}>
  <List aria-label="Primary navigation" sx={{ px: 1 }}>
    <NavigationLink to="/" label="Dashboard" onNavigate={onNavigate} />
    <NavigationGroup label="Billing" links={[["/monthly-bill-review", "Monthly Bill Review"], ["/billing", "Billing Batches"], ["/exception-review", "Exception Review"]]} onNavigate={onNavigate} />
    <NavigationGroup label="Masters" links={[["/employees", "Employees"], ["/mobile-allocations", "Mobile Allocations"], ["/factories", "Factories"], ["/departments", "Departments"], ["/designations", "Designations"], ["/categories", "Categories"], ["/providers", "Telecom Providers"]]} onNavigate={onNavigate} />
    <NavigationGroup label="Reports" links={[["/reports/monthly-bill", "Monthly Bill Report"], ["/reports/vas", "VAS Report"]]} onNavigate={onNavigate} />
    {isAdministrator && <NavigationGroup label="Administration" links={[["/admin/users", "User Management"], ["/admin/password-resets", "Password Resets"]]} onNavigate={onNavigate} />}
  </List>
  <Box sx={{ mt: 'auto', mx: 2, py: 2, borderTop: '1px solid rgba(255,255,255,0.18)', color: '#fff', fontSize: 13.5, fontWeight: 700, letterSpacing: '0.02em', textAlign: 'center' }}>
    Powered By Concord IT
  </Box>
  </Box>
}

function NavigationGroup({ label, links, onNavigate }: { label: string; links: string[][]; onNavigate?: () => void }) {
  return <>
    <ListSubheader disableSticky sx={{ lineHeight: 2.5, mt: 1, bgcolor: 'transparent', color: 'rgba(255,255,255,0.94)', fontSize: 11.5, fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.09em', '&::after': { content: '""', display: 'block', height: 2, width: 24, mt: -0.5, borderRadius: 1, backgroundImage: `linear-gradient(90deg, ${brand.yellow}, ${brand.orange}, ${brand.red})` } }}>{label}</ListSubheader>
    {links.map(([to, linkLabel]) => <NavigationLink key={`${label}-${linkLabel}`} to={to} label={linkLabel} onNavigate={onNavigate} />)}
  </>
}

// White icon chips on the coloured sidebar; the selected item turns into a white pill with a gradient chip.
const navItemSx = {
  mb: 0.25,
  color: '#fff',
  borderRadius: 2.5,
  py: 1.1,
  position: 'relative',
  transition: 'background-color .18s ease, transform .18s ease, box-shadow .18s ease',
  '&:hover': { backgroundColor: 'rgba(255,255,255,0.12)', transform: 'translateX(2px)', boxShadow: '0 5px 17px rgba(40,10,50,0.17)' },
  '&.Mui-selected': {
    color: '#78172C',
    fontWeight: 700,
    backgroundImage: 'linear-gradient(90deg, #fff 0%, #fff5f7 63%, #fffaf1 100%)',
    backgroundColor: '#fff',
    boxShadow: '0 7px 19px rgba(45,8,41,0.22)',
    '&::before': { content: '""', position: 'absolute', left: 0, top: 8, bottom: 8, width: 4, borderRadius: '0 3px 3px 0', backgroundImage: `linear-gradient(180deg, ${brand.yellow}, ${brand.orange}, ${brand.red}, ${brand.magenta})` },
    '& .MuiListItemText-primary': { fontWeight: 700 },
  },
  '&.Mui-selected:hover': { backgroundColor: '#fff', transform: 'none' },
  '&.Mui-selected .nav-icon-chip': { backgroundImage: `linear-gradient(135deg, ${brand.red}, ${brand.magenta})`, color: '#fff', boxShadow: '0 4px 10px rgba(105,21,55,0.29)' },
} as const

const navIconSx = {
  minWidth: 0,
  width: 28,
  height: 28,
  mr: 1.5,
  borderRadius: 2,
  alignItems: 'center',
  justifyContent: 'center',
  color: '#8C2136',
  backgroundColor: 'rgba(255,255,255,0.92)',
} as const

function NavigationLink({ to, label, onNavigate }: { to: string; label: string; onNavigate?: () => void }) {
  const location = useLocation()
  const selected = navigationTarget(location.pathname) === to
  return <ListItemButton component={RouterLink} to={to} selected={selected} onClick={onNavigate} sx={navItemSx}>
    <ListItemIcon className="nav-icon-chip" sx={navIconSx}>{iconByPath[to] ?? <DashboardIcon fontSize="small" />}</ListItemIcon>
    <ListItemText primary={label} slotProps={{ primary: { sx: { fontSize: 14 } } }} />
  </ListItemButton>
}
