/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./src/**/*.{html,ts}"],
  theme: {
    extend: {
      fontFamily: {
        display: ['Space Grotesk', 'Inter', 'system-ui', 'sans-serif'],
        sans: ['Inter', 'system-ui', 'sans-serif'],
        mono: ['JetBrains Mono', 'monospace']
      },
      colors: {
        // Primary brand color - indigo/violet. Every existing `forge-*` utility class
        // across the app repaints from this scale, so re-theming lives in one place.
        forge: {
          50: '#F5F3FF',
          100: '#EDE9FE',
          200: '#DDD6FE',
          300: '#C4B5FD',
          400: '#A78BFA',
          500: '#7C6AEF',
          600: '#6449E0',
          700: '#5238C4',
          800: '#432F9E',
          900: '#362878'
        },
        // Secondary accent - teal. Used for highlights that need to read as distinct
        // from the primary brand color (marketing page, secondary CTAs, badges).
        accent: {
          50: '#ECFDF7',
          100: '#D1FAEA',
          200: '#A5F3D5',
          300: '#6EE7BE',
          400: '#34D399',
          500: '#0EA97C',
          600: '#0B8A66',
          700: '#0A6F54',
          800: '#0A5945',
          900: '#094A3A'
        },
        // Surfaces: a cool, lavender-tinted neutral scale (inspired by Catppuccin Latte) instead of
        // flat white/gray, so panels, columns and the page background separate softly.
        canvas: '#F3F2F9',
        panel: '#FFFFFF',
        sunken: '#ECEBF5',
        line: { DEFAULT: '#E4E2EF', strong: '#D2CFE3' },
        // Text tones, tinted to match the surfaces rather than neutral gray.
        ink: { DEFAULT: '#25223A', soft: '#4B4768', muted: '#6E6A86', faint: '#9D99B3' },
        // Status tones: color is reserved for meaning. Each has a readable foreground, a soft tint
        // for chip backgrounds and a hairline border.
        tone: {
          crit: { fg: '#B3123A', bg: '#FDE9EE', line: '#F6C3D0' },
          high: { fg: '#B4510A', bg: '#FEEFE0', line: '#F8D0A8' },
          med:  { fg: '#8A6508', bg: '#FBF3D6', line: '#EEDB96' },
          low:  { fg: '#1F6F8B', bg: '#E3F2F7', line: '#B4D9E6' },
          ok:   { fg: '#23723B', bg: '#E4F5E8', line: '#B6DFC1' }
        }
      },
      boxShadow: {
        card: '0 1px 2px rgba(37, 34, 58, 0.06), 0 1px 1px rgba(37, 34, 58, 0.04)',
        lift: '0 6px 16px -4px rgba(37, 34, 58, 0.16), 0 2px 4px rgba(37, 34, 58, 0.06)'
      }
    }
  },
  plugins: []
};
