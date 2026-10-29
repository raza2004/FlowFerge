/** @type {import('tailwindcss').Config} */

// "Ember Editorial": warm bone paper, ink black, one ember orange. Defined once here so a re-theme is one file.
const ember = {
  50: '#FFF3ED',
  100: '#FFE3D5',
  200: '#FFC6AB',
  300: '#FFA074',
  400: '#F97C47',
  500: '#F2501C',
  600: '#D93F0F',
  700: '#B3320C',
  800: '#8A2810',
  900: '#5E1C0C'
};

module.exports = {
  content: ["./src/**/*.{html,ts}"],
  theme: {
    extend: {
      fontFamily: {
        // Instrument Serif has a single weight: never faux-bold it (see .font-display in styles.scss).
        display: ['"Instrument Serif"', 'Georgia', 'serif'],
        sans: ['Geist', 'system-ui', 'sans-serif'],
        mono: ['"Geist Mono"', 'ui-monospace', 'monospace']
      },
      colors: {
        ember,
        // Legacy names. The admin app said `admin-*`, the web app `forge-*`; both resolve to ember.
        forge: ember,
        admin: ember,
        // Success and "done" states. Green is reserved for things that went well.
        accent: {
          50: '#EDF5E6',
          100: '#DBEACD',
          200: '#BFD9AE',
          300: '#9CC285',
          400: '#6FA25A',
          500: '#3F8442',
          600: '#2D6A32',
          700: '#245628',
          800: '#1D4421',
          900: '#17361A'
        },
        // Surfaces: warm paper, not white and not grey.
        canvas: '#EFEBE3',
        panel: '#FAF8F3',
        sunken: '#E6E1D6',
        line: { DEFAULT: '#DAD4C7', strong: '#C6BFAE' },
        // Text tones, tinted warm. Never flat grey.
        ink: { DEFAULT: '#17140F', soft: '#3B362D', muted: '#6A6256', faint: '#978F7F' },
        // Status tones: colour is reserved for meaning.
        tone: {
          crit: { fg: '#B42318', bg: '#FBE7E3', line: '#F2BDB4' },
          high: { fg: '#A8480A', bg: '#FDEBD9', line: '#F4CBA3' },
          med:  { fg: '#7D5C06', bg: '#F7EDC8', line: '#E8D78F' },
          low:  { fg: '#1F5F7A', bg: '#E1EEF2', line: '#B3D3DF' },
          ok:   { fg: '#2D6A32', bg: '#E6F0DC', line: '#BFD9AE' }
        }
      },
      boxShadow: {
        card: '0 1px 0 rgba(23, 20, 15, 0.05)',
        lift: '0 14px 34px -14px rgba(23, 20, 15, 0.28), 0 2px 4px rgba(23, 20, 15, 0.06)'
      },
      borderRadius: { xl: '0.875rem' },
      transitionTimingFunction: { out: 'cubic-bezier(0.22, 1, 0.36, 1)' }
    }
  },
  plugins: []
};
