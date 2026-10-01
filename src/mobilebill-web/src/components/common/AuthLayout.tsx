import { Box, Typography } from '@mui/material'
import { keyframes } from '@mui/material/styles'
import type { ReactNode } from 'react'

import gustonLogo from '../../assets/guston-logo.webp'

const heroIn = keyframes`from { opacity: 0; transform: translateY(17px) } to { opacity: 1; transform: none }`
const cardIn = keyframes`from { opacity: 0; transform: translateX(22px) scale(.985) } to { opacity: 1; transform: none }`
const decorFloat = keyframes`0%, 100% { transform: translate(0, 0) scale(1) } 50% { transform: translate(14px, -12px) scale(1.05) }`
const ring = keyframes`0%, 100% { transform: translate(0, 0) rotate(0deg) } 50% { transform: translate(-15px, 12px) rotate(8deg) }`
const bar = keyframes`0%, 100% { transform: scaleY(.9); opacity: .65 } 50% { transform: scaleY(1.08); opacity: 1 }`
const spark = keyframes`0%, 100% { opacity: .25; transform: rotate(45deg) scale(.7) } 50% { opacity: 1; transform: rotate(45deg) scale(1.15) }`
const sweep = keyframes`0% { left: -130% } 100% { left: 135% }`

const wide = '@media (min-width: 1051px)'
const phone = '@media (max-width: 620px)'

const pills = ['Monthly bill processing', 'Employee allocations', 'Exception review', 'Approval workflow']

function HeroDecoration() {
  const orb = { position: 'absolute', borderRadius: '50%', filter: 'blur(1px)', opacity: 0.55, animation: `${decorFloat} 8s ease-in-out infinite` } as const
  const circle = { position: 'absolute', border: '1px solid rgba(255,255,255,0.12)', borderRadius: '50%', animation: `${ring} 12s ease-in-out infinite` } as const
  const diamond = { position: 'absolute', width: 8, height: 8, transform: 'rotate(45deg)', boxShadow: '0 0 14px rgba(255,201,40,.55)', animation: `${spark} 2.6s ease-in-out infinite` } as const
  return <Box aria-hidden="true" sx={{ position: 'absolute', inset: 0, overflow: 'hidden', pointerEvents: 'none', zIndex: 1 }}>
    <Box sx={{ ...orb, width: 240, height: 240, left: -80, top: 40, background: 'radial-gradient(circle at 42% 42%, rgba(255,201,40,.40), rgba(226,27,45,.08) 52%, transparent 72%)' }} />
    <Box sx={{ ...orb, width: 330, height: 330, right: -150, top: 135, animationDelay: '-2s', background: 'radial-gradient(circle, rgba(216,43,120,.34), rgba(122,61,184,.08) 55%, transparent 72%)' }} />
    <Box sx={{ ...orb, width: 300, height: 300, right: '7%', bottom: -175, animationDelay: '-4s', background: 'radial-gradient(circle, rgba(255,201,40,.18), rgba(216,43,120,.15) 40%, rgba(122,61,184,.12) 58%, transparent 73%)' }} />
    <Box sx={{ ...circle, width: 390, height: 390, right: -170, top: -95 }} />
    <Box sx={{ ...circle, width: 620, height: 620, left: -330, bottom: -410, borderColor: 'rgba(255,201,40,0.10)', animationDelay: '-5s' }} />
    <Box sx={{
      position: 'absolute', right: '9%', bottom: '13%', width: 240, height: 170, opacity: 0.2,
      backgroundImage: 'linear-gradient(rgba(255,255,255,.17) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,.17) 1px, transparent 1px)',
      backgroundSize: '24px 24px',
      maskImage: 'linear-gradient(135deg, transparent, #000 28%, #000 70%, transparent)',
      transform: 'perspective(500px) rotateY(-18deg) rotateX(14deg) rotateZ(-4deg)',
    }} />
    <Box sx={{ position: 'absolute', right: '8%', top: '27%', width: 120, height: 110, display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: '9px', opacity: 0.38, transform: 'rotate(-9deg)' }}>
      {[35, 50, 69, 55, 82, 66].map((height, index) => <Box key={index} sx={{ width: 12, height: `${height}%`, borderRadius: '10px 10px 2px 2px', background: 'linear-gradient(180deg, #ffc928, #f39a18, #e21b2d)', animation: `${bar} 2.8s ease-in-out infinite`, animationDelay: `${(index + 1) * 0.1}s` }} />)}
    </Box>
    <Box sx={{ ...diamond, left: '8%', top: '31%', bgcolor: '#ffc928' }} />
    <Box sx={{ ...diamond, left: '46%', top: '12%', bgcolor: '#f39a18', animationDelay: '.8s' }} />
    <Box sx={{ ...diamond, right: '14%', bottom: '29%', bgcolor: '#f08bb5', animationDelay: '1.4s' }} />
  </Box>
}

const primaryGradient = 'linear-gradient(100deg, #e21b2d 0%, #d82b78 48%, #7a3db8 100%)'

// Split sign-in layout shared by the login, register and password reset pages: a brand hero on the left, the form card on the right.
export function AuthLayout({ children }: { children: ReactNode; maxWidth?: number }) {
  return <Box sx={{
    minHeight: '100vh',
    display: 'grid',
    gridTemplateColumns: '1fr',
    bgcolor: '#6d1d42',
    [wide]: { gridTemplateColumns: 'minmax(0, 58%) minmax(500px, 42%)', height: '100vh', overflow: 'hidden' },
    '@media (prefers-reduced-motion: reduce)': { '& *, & *::before, & *::after': { animation: 'none !important', transition: 'none !important' } },
  }}>
    <Box component="section" sx={{
      position: 'relative', display: 'flex', alignItems: 'center', overflow: 'hidden', isolation: 'isolate', color: '#fff',
      minHeight: 500, px: '6vw', py: 5, pb: 12,
      background: [
        'radial-gradient(circle at 0% 10%, rgba(226,27,45,.34), transparent 28%)',
        'radial-gradient(circle at 18% 96%, rgba(255,201,40,.12), transparent 24%)',
        'radial-gradient(circle at 88% 18%, rgba(216,43,120,.34), transparent 30%)',
        'radial-gradient(circle at 86% 95%, rgba(122,61,184,.34), transparent 31%)',
        'linear-gradient(118deg, #69192f 0%, #8e2248 35%, #9c2b64 64%, #6f2d78 100%)',
      ].join(', '),
      [wide]: { height: '100vh', pr: 'clamp(40px, 5.2vw, 100px)', pl: 'clamp(48px, 6vw, 112px)' },
      [phone]: { px: 2.5, minHeight: 'auto' },
    }}>
      <HeroDecoration />
      <Box sx={{ width: '100%', maxWidth: 860, position: 'relative', zIndex: 4, textAlign: 'left' }}>
        <Box component="img" src={gustonLogo} alt="Guston Ltd" sx={{ display: 'block', height: { xs: 68, sm: 82 }, width: 'auto', mb: 3, filter: 'brightness(0) invert(1) drop-shadow(0 5px 9px rgba(0,0,0,.10))', animation: `${heroIn} .55s .05s cubic-bezier(.2,.75,.25,1) both` }} />
        <Typography component="div" sx={{ fontSize: 11, letterSpacing: '0.16em', fontWeight: 800, mb: 1.25, color: '#ffd9e2', animation: `${heroIn} .5s .15s ease both` }}>ENTERPRISE MOBILE BILLING</Typography>
        <Typography component="div" sx={{ m: 0, mb: 2.75, fontSize: { xs: 48, sm: 'clamp(58px, 6.25vw, 112px)' }, lineHeight: 0.91, letterSpacing: '-0.052em', fontWeight: 800, color: '#fff', animation: `${heroIn} .58s .23s cubic-bezier(.2,.75,.25,1) both` }}>
          Mobile Bill<br />
          <Box component="span" sx={{ background: 'linear-gradient(90deg, #fff 0%, #ffd5de 26%, #ffc928 48%, #f06a98 70%, #c58be5 100%)', WebkitBackgroundClip: 'text', backgroundClip: 'text', color: 'transparent' }}>Management</Box>
        </Typography>
        <Typography component="p" sx={{ maxWidth: 790, m: 0, mb: 3, fontSize: { xs: 13, sm: 17 }, lineHeight: 1.48, color: '#f1dce5', animation: `${heroIn} .55s .31s cubic-bezier(.2,.75,.25,1) both` }}>
          A centralized workspace for monthly corporate mobile bill processing, employee allocations, billing exceptions, approvals, reporting and audit-ready review.
        </Typography>
        <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1, animation: `${heroIn} .5s .39s cubic-bezier(.2,.75,.25,1) both` }}>
          {pills.map(label => <Box component="span" key={label} sx={{ px: 1.5, py: 1, border: '1px solid rgba(255,255,255,.20)', borderRadius: 999, bgcolor: 'rgba(74,13,41,.18)', color: '#fff', fontSize: 12, backdropFilter: 'blur(8px)', transition: '.18s ease', '&:hover': { transform: 'translateY(-2px)', bgcolor: 'rgba(255,255,255,.12)' } }}>{label}</Box>)}
        </Box>
      </Box>
      <Box aria-label="Powered by Concord IT" sx={{ position: 'absolute', left: { xs: 20, sm: 'clamp(48px, 6vw, 112px)' }, bottom: { xs: 28, sm: 42 }, display: 'flex', alignItems: 'center', gap: 1.5, color: 'rgba(255,255,255,.9)', fontSize: { xs: 12, sm: 15 }, fontWeight: 600, letterSpacing: '0.01em', zIndex: 20 }}>
        <Box component="span" sx={{ width: { xs: 25, sm: 34 }, height: { xs: 2, sm: 3 }, borderRadius: 1, background: 'linear-gradient(90deg, #ffc928, #f39a18, #e21b2d, #d82b78)', boxShadow: '0 0 11px rgba(255,201,40,.28)' }} />
        <span>Powered by <Box component="strong" sx={{ color: '#fff', fontWeight: 800, fontSize: { xs: 13, sm: 16 } }}>Concord IT</Box></span>
      </Box>
    </Box>

    <Box sx={{
      display: 'flex', alignItems: 'center', justifyContent: 'center', p: { xs: 1.75, sm: 3 },
      background: 'radial-gradient(circle at 100% 0%, rgba(216,43,120,.08), transparent 28%), linear-gradient(180deg, #fff8fa 0%, #f7eef5 100%)',
      minHeight: 480,
      [wide]: { height: '100vh', minHeight: 0, pr: 'clamp(24px, 4vw, 76px)' },
    }}>
      {/* data-light keeps the form on the light palette even when the rest of the app is in dark mode. */}
      <Box data-light="" sx={{
        width: 'min(540px, 100%)', boxSizing: 'border-box', overflowY: 'auto',
        [wide]: { maxHeight: 'calc(100vh - 48px)' },
        bgcolor: 'rgba(255,255,255,.985)', color: '#14233d', border: '1px solid #ead9e2', borderRadius: '23px', p: { xs: '26px 18px 22px', sm: '34px 32px 27px' },
        boxShadow: '0 22px 60px rgba(76,18,53,.22)',
        animation: `${cardIn} .62s .12s cubic-bezier(.2,.75,.25,1) both`,
        '& .MuiTypography-h4': { fontSize: 31, lineHeight: 1.1, color: '#12223e', letterSpacing: '-0.025em', fontWeight: 700 },
        '& .MuiTypography-h4 + .MuiTypography-root': { fontSize: 13.5, lineHeight: 1.48, color: '#707c90' },
        '& .MuiOutlinedInput-root': {
          borderRadius: '10px', bgcolor: '#fffafc', color: '#19263c', transition: 'border-color .2s ease, box-shadow .2s ease, background .2s ease',
          '& fieldset': { borderColor: '#decbd5' },
          '&:hover fieldset': { borderColor: '#d0b2c0' },
          '&.Mui-focused': { bgcolor: '#fff', boxShadow: '0 0 0 4px rgba(207,53,100,.10)' },
          '&.Mui-focused fieldset': { borderColor: '#cf3564', borderWidth: 1 },
        },
        // Browser autofill paints inputs solid blue; keep the field's own tint and text colour instead.
        '& .MuiOutlinedInput-input:-webkit-autofill': {
          WebkitBoxShadow: '0 0 0 100px #fffafc inset',
          boxShadow: '0 0 0 100px #fffafc inset',
          WebkitTextFillColor: '#19263c',
          caretColor: '#19263c',
          borderRadius: 'inherit',
          transition: 'background-color 9999s ease-in-out 0s',
        },
        '& .MuiOutlinedInput-input:-webkit-autofill:focus': {
          WebkitBoxShadow: '0 0 0 100px #fff inset',
          boxShadow: '0 0 0 100px #fff inset',
        },
        '& .MuiInputLabel-root.Mui-focused': { color: '#b11942' },
        '& .MuiFormHelperText-root': { color: '#54657f' },
        '& form .MuiButton-contained': { mt: 3 },
        '& .MuiButton-contained': {
          position: 'relative', overflow: 'hidden', height: 50, borderRadius: '11px', color: '#fff', fontSize: 14.5, fontWeight: 800,
          backgroundImage: primaryGradient,
          boxShadow: '0 12px 28px rgba(192,30,76,.24)',
          transition: 'transform .18s ease, filter .18s ease, box-shadow .18s ease',
          '&::after': { content: '""', position: 'absolute', top: 0, bottom: 0, width: '30%', left: '-130%', transform: 'skewX(-18deg)', background: 'linear-gradient(90deg, transparent, rgba(255,255,255,.22), transparent)' },
          '&:hover': { backgroundImage: primaryGradient, filter: 'brightness(1.04)', transform: 'translateY(-2px)', boxShadow: '0 15px 31px rgba(192,30,76,.27)' },
          '&:hover::after': { animation: `${sweep} .7s ease` },
          '&:active': { transform: 'scale(.985)' },
          '&.Mui-disabled': { opacity: 0.9, color: '#fff', backgroundImage: primaryGradient },
        },
        '& form > .MuiTypography-body2': { textAlign: 'center', fontSize: 14, fontWeight: 700, color: '#9b8b93', mt: 1 },
        '& a, & .MuiLink-root': { color: '#b11942', fontWeight: 700, textDecoration: 'none', transition: 'color .18s ease', '&:hover': { color: '#d3173e' } },
      }}>
        {children}
        <Typography sx={{ textAlign: 'center', color: '#927f8c', fontSize: 11, mt: 1.5 }}>Secure enterprise authentication • Mobile Bill Management</Typography>
      </Box>
    </Box>
  </Box>
}
