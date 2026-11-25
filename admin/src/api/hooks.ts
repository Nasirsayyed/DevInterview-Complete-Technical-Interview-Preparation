import { useQuery } from '@tanstack/react-query';
import { api } from './client';
import type { AdminCategory, AdminTechnology } from './types';

/** Shared lookup data for selects. */
export function useTechnologies() {
  return useQuery({ queryKey: ['admin', 'technologies'], queryFn: () => api.get<AdminTechnology[]>('/admin/technologies') });
}

export function useCategories(technologyId?: string | null) {
  return useQuery({
    queryKey: ['admin', 'categories', technologyId ?? 'all'],
    queryFn: () => api.get<AdminCategory[]>('/admin/categories', { technologyId: technologyId ?? undefined }),
  });
}

export const toOptions = <T extends { id: string; name: string }>(items: T[] | undefined) => (items ?? []).map((i) => ({ value: i.id, label: i.name }));
