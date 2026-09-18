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
        }
      }
    }
  },
  plugins: []
};
