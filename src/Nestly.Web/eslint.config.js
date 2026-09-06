import js from '@eslint/js';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  { ignores: ['dist'] },
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,

      // Type-aware rules, the same trade the .NET side makes with StyleCop and Sonar: slower
      // than syntax-only linting, and the only kind that catches a floating promise.
      ...tseslint.configs.strictTypeChecked,
      ...tseslint.configs.stylisticTypeChecked,
      reactHooks.configs.flat['recommended-latest'],
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      ecmaVersion: 2022,
      globals: globals.browser,
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    rules: {
      // Neither is in the preset sets, and both are things the .NET side would never allow past
      // a build: a loose comparison, and diagnostics written somewhere the user cannot see.
      eqeqeq: ['error', 'always'],
      'no-console': 'error',
    },
  },
);
