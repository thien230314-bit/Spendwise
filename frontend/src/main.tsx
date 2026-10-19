import {StrictMode} from 'react';
import {createRoot} from 'react-dom/client';
import App from './App.tsx';
import './index.css';
import { runOneTimeCleanReset } from './utils/cleanStateReset';

// Execute one-time clean state migration before app tree hydration
runOneTimeCleanReset();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
