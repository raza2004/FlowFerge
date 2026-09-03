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
        admin: {
          50: '#F5F4FE',
          100: '#EEEDFE',
          200: '#DDDAFC',
          300: '#C0BAF8',
          400: '#9D92F1',
          500: '#7A6CE8',
          600: '#5E4FD6',
          700: '#534AB7',
          800: '#443E93',
          900: '#3C3489'
        }
      }
    }
  },
  plugins: []
};
