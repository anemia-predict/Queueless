import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { BrowserRouter } from 'react-router-dom';
import { Home } from '../pages/public/Home';
import { AuthProvider } from '../contexts/AuthContext';

describe('Home Page', () => {
  it('renders QueueLess hero heading and primary buttons', () => {
    render(
      <BrowserRouter>
        <AuthProvider>
          <Home />
        </AuthProvider>
      </BrowserRouter>
    );

    expect(screen.getByText(/Say goodbye to waiting rooms with/i)).toBeInTheDocument();
    expect(screen.getByText(/Find Hospitals & Doctors/i)).toBeInTheDocument();
    expect(screen.getByText(/Register a Hospital/i)).toBeInTheDocument();
  });
});
