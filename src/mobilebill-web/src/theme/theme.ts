import { alpha, createTheme } from '@mui/material/styles'

// Dialog-inspired brand palette: vivid red with orange / yellow / magenta / purple accents.
export const brand = {
  red: '#E21B2D',
  redDark: '#BD1022',
  redDeep: '#C91428',
  orange: '#F39A18',
  yellow: '#FFC928',
  magenta: '#D82B78',
  purple: '#7A3DB8',
  indigo: '#4A3F9F',
  ink: '#0B1B37',
  link: '#B51732',
  linkOnDark: '#FF8A98',
}

// KPI numbers cycle through these (light, dark) so cards are easy to tell apart.
export const kpiColors = [
  ['#C51B30', '#FF6B7A'],
  ['#A81C57', '#F05FA0'],
  ['#C46B00', '#F5A03A'],
  ['#8A3CAE', '#C08CE8'],
] as const

export const brandGradients = {
  // Top bar and stripes: red → magenta → purple.
  topbar: `linear-gradient(100deg, ${brand.redDeep} 0%, ${brand.red} 42%, ${brand.magenta} 76%, ${brand.purple} 100%)`,
  stripe: `linear-gradient(90deg, ${brand.yellow} 0%, ${brand.orange} 23%, ${brand.red} 45%, ${brand.magenta} 70%, ${brand.purple} 100%)`,
  accentVertical: `linear-gradient(180deg, ${brand.red}, ${brand.magenta}, ${brand.purple})`,
  button: `linear-gradient(135deg, #D9162B, ${brand.red} 48%, #C62D68)`,
  sidebar: [
    `radial-gradient(circle at 90% 7%, ${alpha(brand.yellow, 0.2)}, transparent 24%)`,
    `radial-gradient(circle at 4% 48%, ${alpha(brand.magenta, 0.2)}, transparent 23%)`,
    `radial-gradient(circle at 94% 87%, ${alpha(brand.purple, 0.22)}, transparent 25%)`,
    'linear-gradient(180deg, #7C1830 0%, #8F2446 30%, #7C2B67 62%, #4B287F 100%)',
  ].join(', '),
  page: [
    `radial-gradient(circle at 0% 0%, ${alpha(brand.red, 0.055)}, transparent 24%)`,
    `radial-gradient(circle at 100% 10%, ${alpha(brand.purple, 0.055)}, transparent 24%)`,
    'linear-gradient(180deg, #F8F8FC 0%, #F4F5FA 100%)',
  ].join(', '),
  pageDark: [
    `radial-gradient(circle at 0% 0%, ${alpha(brand.red, 0.1)}, transparent 26%)`,
    `radial-gradient(circle at 100% 10%, ${alpha(brand.purple, 0.12)}, transparent 26%)`,
    'linear-gradient(180deg, #12131C 0%, #0E0F17 100%)',
  ].join(', '),
}

const cardShadow = '0 1px 3px rgba(15,23,42,0.10), 0 6px 18px rgba(15,23,42,0.05)'
const dialogShadow = '0 4px 6px rgba(16,24,40,0.08), 0 12px 24px rgba(16,24,40,0.14)'

export const theme = createTheme({
  cssVariables: { colorSchemeSelector: 'data' },
  colorSchemes: {
    light: {
      palette: {
        primary: { main: brand.red, dark: brand.redDark, light: '#FFF1F4', contrastText: '#FFFFFF' },
        secondary: { main: brand.purple, contrastText: '#FFFFFF' },
        info: { main: brand.indigo },
        success: { main: '#16823A' },
        warning: { main: '#CF6900' },
        // Deliberately darker than the brand red so errors read differently from primary actions.
        error: { main: '#9B111E' },
        text: { primary: brand.ink, secondary: '#5B6472' },
        background: { default: '#F5F6FA', paper: '#FFFFFF' },
        divider: 'rgba(11,27,55,0.12)',
      },
    },
    dark: {
      palette: {
        primary: { main: '#FF5468', dark: brand.red, light: '#3A1620', contrastText: '#FFFFFF' },
        secondary: { main: '#B58BEA', contrastText: '#12131C' },
        info: { main: '#9A90E8' },
        success: { main: '#4CC46F' },
        warning: { main: '#F5A03A' },
        error: { main: '#FF7A85' },
        text: { primary: '#F1F2F8', secondary: '#A9AFC0' },
        background: { default: '#0E0F17', paper: '#181925' },
        divider: 'rgba(255,255,255,0.12)',
      },
    },
  },
  shape: { borderRadius: 10 },
  typography: {
    fontFamily: 'Arial, Helvetica, sans-serif',
    h4: { fontSize: '1.75rem', fontWeight: 600 },
    h5: { fontSize: '1.375rem', fontWeight: 600 },
    h6: { fontSize: '1.125rem', fontWeight: 600 },
    subtitle1: { fontSize: '0.9375rem', fontWeight: 600 },
    body2: { fontSize: '0.84375rem' },
    caption: { fontSize: '0.75rem' },
    button: { fontSize: '0.875rem', fontWeight: 600, textTransform: 'none' },
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: (theme) => ({
        body: { backgroundImage: brandGradients.page, backgroundAttachment: 'fixed', ...theme.applyStyles('dark', { backgroundImage: brandGradients.pageDark }) },
        a: { color: brand.link, ...theme.applyStyles('dark', { color: brand.linkOnDark }) },
      }),
    },
    MuiLink: {
      styleOverrides: { root: ({ theme }) => ({ color: brand.link, ...theme.applyStyles('dark', { color: brand.linkOnDark }) }) },
    },
    MuiButton: {
      styleOverrides: {
        root: { borderRadius: 8, minHeight: 36 },
        sizeSmall: { minHeight: 32 },
      },
      variants: [
        {
          props: { variant: 'contained', color: 'primary' },
          style: {
            backgroundImage: brandGradients.button,
            color: '#FFFFFF',
            boxShadow: `0 4px 9px ${alpha(brand.red, 0.22)}`,
            '&:hover': { backgroundImage: brandGradients.button, filter: 'brightness(1.04)', boxShadow: `0 7px 15px ${alpha(brand.red, 0.25)}` },
            '&.Mui-disabled': { backgroundImage: 'none' },
          },
        },
        {
          props: { variant: 'outlined', color: 'primary' },
          style: ({ theme }) => ({
            backgroundColor: '#FFF1F4', borderColor: '#F1C4CF', color: '#A01839',
            '&:hover': { backgroundColor: '#FFE4EA', borderColor: '#EBAABB' },
            ...theme.applyStyles('dark', {
              backgroundColor: alpha(brand.red, 0.12), borderColor: alpha(brand.red, 0.45), color: brand.linkOnDark,
              '&:hover': { backgroundColor: alpha(brand.red, 0.2), borderColor: alpha(brand.red, 0.6) },
            }),
          }),
        },
        {
          props: { variant: 'text', color: 'primary' },
          style: ({ theme }) => ({ color: brand.link, ...theme.applyStyles('dark', { color: brand.linkOnDark }) }),
        },
      ],
    },
    MuiCard: {
      defaultProps: { variant: 'outlined' },
    },
    MuiTextField: { defaultProps: { variant: 'outlined' } },
    // By default the option list opens on top of the field, lined up with the current value, so the
    // release of the opening click can land on an option and pick it at once. Open it below instead.
    MuiSelect: {
      defaultProps: {
        MenuProps: {
          anchorOrigin: { vertical: 'bottom', horizontal: 'left' },
          transformOrigin: { vertical: 'top', horizontal: 'left' },
          slotProps: { paper: { sx: { mt: 0.5, maxHeight: 360 } } },
        },
      },
    },
    MuiOutlinedInput: { styleOverrides: { root: { borderRadius: 8 } } },
    MuiChip: { styleOverrides: { root: { borderRadius: 999, fontWeight: 600 } } },
    MuiDialog: {
      styleOverrides: {
        paper: { borderRadius: 12, boxShadow: dialogShadow },
      },
    },
    MuiTableCell: {
      styleOverrides: {
        head: ({ theme }) => ({ fontWeight: 700, color: '#11284B', backgroundColor: '#F6F7FB', ...theme.applyStyles('dark', { color: '#E6E8F2', backgroundColor: '#20212E' }) }),
      },
    },
    MuiTableRow: {
      styleOverrides: {
        root: ({ theme }) => ({
          '&.MuiTableRow-hover:hover': { backgroundColor: '#F8FAFF' },
          ...theme.applyStyles('dark', { '&.MuiTableRow-hover:hover': { backgroundColor: 'rgba(255,255,255,0.05)' } }),
        }),
      },
    },
    MuiAppBar: {
      styleOverrides: {
        root: { backgroundImage: brandGradients.topbar, color: '#FFFFFF', boxShadow: '0 2px 9px rgba(104,23,64,0.20)' },
      },
    },
    MuiTab: {
      styleOverrides: { root: { fontWeight: 700 } },
    },
    MuiTabs: {
      styleOverrides: { indicator: { height: 2, backgroundImage: 'linear-gradient(90deg, #E21B2D, #D82B78, #7A3DB8)' } },
    },
    MuiStepIcon: {
      styleOverrides: {
        root: { '&.Mui-active, &.Mui-completed': { color: brand.magenta } },
      },
    },
    MuiListItemButton: {
      styleOverrides: {
        root: {
          borderRadius: 8,
          '&.Mui-selected': {
            backgroundColor: alpha(brand.red, 0.1),
            color: '#78172C',
          },
          '&.Mui-selected:hover': { backgroundColor: alpha(brand.red, 0.14) },
          '&.Mui-selected .MuiListItemIcon-root': { color: brand.red },
        },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: { backgroundImage: 'none' },
      },
      variants: [
        {
          props: { variant: 'outlined' },
          style: ({ theme }) => ({
            borderRadius: 10, borderColor: '#E4E7EE', boxShadow: cardShadow,
            ...theme.applyStyles('dark', { borderColor: 'rgba(255,255,255,0.10)', boxShadow: 'none' }),
          }),
        },
      ],
    },
  },
})
