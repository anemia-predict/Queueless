import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { StatCard } from '../components/StatCard';

describe('StatCard Component', () => {
  it('renders title, value and subtitle correctly', () => {
    render(<StatCard title="Active Queues" value="12" subtitle="Across 3 hospitals" />);
    expect(screen.getByText('Active Queues')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
    expect(screen.getByText('Across 3 hospitals')).toBeInTheDocument();
  });
});
