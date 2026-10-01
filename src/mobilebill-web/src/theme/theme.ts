import { alpha, createTheme } from '@mui/material/styles'

const cardShadow = '0 1px 2px rgba(16,24,40,0.06), 0 1px 3px rgba(16,24,40,0.10)'
const dialogShadow = '0 4px 6px rgba(16,24,40,0.08), 0 12px 24px rgba(16,24,40,0.14)'

export const theme = createTheme({
  palette: {
    primary: { main: '#3949AB', dark: '#303F9F', light: '#E8EAF6', contrastText: '#FFFFFF' },
    secondary: { main: '#00897B', contrastText: '#FFFFFF' },
    success: { main: '#2E7D32' },
    warning: { main: '#ED6C02' },
    error: { main: '#D32F2F' },
    text: { primary: '#172033', secondary: '#5B6472' },
    background: { default: '#F5F7FB', paper: '#FFFFFF' },
    divider: 'rgba(23,32,51,0.12)',
  },
  shape: { borderRadius: 10 },
  typography: {
    fontFamily: 'Roboto, Arial, sans-serif',
    h4: { fontSize: '1.75rem', fontWeight: 600 },
    h5: { fontSize: '1.375rem', fontWeight: 600 },
    h6: { fontSize: '1.125rem', fontWeight: 600 },
    subtitle1: { fontSize: '0.9375rem', fontWeight: 600 },
    body2: { fontSize: '0.84375rem' },
    caption: { fontSize: '0.75rem' },
    button: { fontSize: '0.875rem', fontWeight: 600, textTransform: 'none' },
  },
  components: {
    MuiButton: {
      styleOverrides: {
        root: { borderRadius: 8, minHeight: 36 },
        sizeSmall: { minHeight: 32 },
      },
    },
    MuiCard: {
      defaultProps: { variant: 'outlined' },
    },
    MuiTextField: { defaultProps: { variant: 'outlined' } },
    MuiOutlinedInput: { styleOverrides: { root: { borderRadius: 8 } } },
    MuiChip: { styleOverrides: { root: { borderRadius: 999, fontWeight: 600 } } },
    MuiDialog: {
      styleOverrides: {
        paper: { borderRadius: 12, boxShadow: dialogShadow },
      },
    },
    MuiTableCell: {
      styleOverrides: {
        head: { fontWeight: 600, backgroundColor: '#F5F7FB' },
      },
    },
    MuiAppBar: {
      styleOverrides: {
        root: { boxShadow: '0 1px 2px rgba(16,24,40,0.08)' },
      },
    },
    MuiListItemButton: {
      styleOverrides: {
        root: {
          borderRadius: 8,
          '&.Mui-selected': {
            backgroundColor: alpha('#3949AB', 0.12),
            color: '#3949AB',
          },
          '&.Mui-selected:hover': { backgroundColor: alpha('#3949AB', 0.16) },
          '&.Mui-selected .MuiListItemIcon-root': { color: '#3949AB' },
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
          style: { borderRadius: 12, borderColor: 'rgba(23,32,51,0.10)', boxShadow: cardShadow },
        },
      ],
    },
  },
})
