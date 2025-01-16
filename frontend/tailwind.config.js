/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./src/**/*.{js,jsx,ts,tsx}"],
  daisyui: {
    themes: ["retro"],
  },
  theme: {
    extend: {},
  },
  plugins: [require('daisyui')],
}

