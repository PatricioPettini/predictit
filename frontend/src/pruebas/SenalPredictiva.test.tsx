import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { SenalPredictiva } from '../componentes/SenalPredictiva';
import type { EquipoEnRiesgoDto } from '../api/tipos';

const equipo = (id: string, score: number): EquipoEnRiesgoDto => ({
  idEquipo: id, codigo: id, score, nivel: 'ALTO', incidencias: 1, motivoPrincipal: 'Fallas recurrentes',
});

describe('Señal predictiva del dashboard', () => {
  it('prioriza el mayor puntaje sin modificar el ranking recibido', () => {
    const equipos = [equipo('NB-COM-007', 77), equipo('PC-ADM-014', 98)];
    render(<MemoryRouter><SenalPredictiva equipos={equipos} puedeVerEquipo /></MemoryRouter>);
    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent('PC-ADM-014');
    expect(equipos[0]?.codigo).toBe('NB-COM-007');
    expect(screen.getByRole('link', { name: 'Ver →' })).toHaveAttribute('href', '/activos/PC-ADM-014');
  });
  it('no presenta cero como predicción cuando no hay equipos evaluados', () => {
    const { container } = render(<MemoryRouter><SenalPredictiva equipos={[]} puedeVerEquipo /></MemoryRouter>);
    expect(container).toBeEmptyDOMElement();
  });
  it('no ofrece navegación a fichas sin permiso', () => {
    render(<MemoryRouter><SenalPredictiva equipos={[equipo('PC-ADM-014', 98)]} puedeVerEquipo={false} /></MemoryRouter>);
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent('PC-ADM-014');
  });
  it('descarta puntajes fuera del contrato en lugar de dibujar una alarma falsa', () => {
    const { container } = render(<MemoryRouter><SenalPredictiva equipos={[equipo('invalid', NaN), equipo('too-high', 101)]} puedeVerEquipo /></MemoryRouter>);
    expect(container).toBeEmptyDOMElement();
  });
});
