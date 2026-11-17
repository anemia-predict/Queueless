import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { Badge } from '../components/Badge';

describe('Badge Component', () => {
  it('renders APPROVED status with green styling', () => {
    render(<Badge status="APPROVED" />);
    const badge = screen.getByText('APPROVED');
    expect(badge).toBeInTheDocument();
    expect(badge.className).toContain('emerald');
  });

  it('renders PENDING status with amber styling', () => {
    render(<Badge status="PENDING" />);
    const badge = screen.getByText('PENDING');
    expect(badge).toBeInTheDocument();
    expect(badge.className).toContain('amber');
  });

  it('renders REJECTED status with rose styling', () => {
    render(<Badge status="REJECTED" />);
    const badge = screen.getByText('REJECTED');
    expect(badge).toBeInTheDocument();
    expect(badge.className).toContain('rose');
  });
});
