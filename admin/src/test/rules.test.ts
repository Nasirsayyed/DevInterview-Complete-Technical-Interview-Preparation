import { describe, expect, it } from 'vitest';
import { validateQuestion } from '../pages/Questions';
import { fromForm } from '../pages/Quizzes';
import { priceToMicros } from '../pages/Subscriptions';
import { sourceProblems } from '../pages/Companies';
import { permissionsFor } from '../auth/AuthContext';
import type { CompanyUpsert, QuestionUpsert } from '../api/types';

const base: QuestionUpsert = {
  technologyId: 't', categoryId: 'c', topicId: null, title: 'What is a record?', body: 'Explain records.', codeSnippet: null, codeLanguage: null,
  type: 'SingleChoice', difficulty: 'Beginner', estimatedSeconds: 60, isPremium: false, status: 'Draft', tags: [],
  options: [{ text: 'A reference type with value equality', isCorrect: true, explanation: null }, { text: 'A struct', isCorrect: false, explanation: null }],
  shortAnswer: '', detailedExplanation: '', referenceAnswer: null,
};

describe('validateQuestion mirrors the backend rules', () => {
  it('accepts a valid single-choice draft without an answer yet', () => {
    expect(validateQuestion(base)).toEqual([]);
  });
  it('requires a short answer before publishing', () => {
    expect(validateQuestion({ ...base, status: 'Published' })).toContain('A short answer is required before publishing.');
  });
  it('choice questions need exactly one correct option', () => {
    const q = { ...base, options: base.options.map((o) => ({ ...o, isCorrect: true })) };
    expect(validateQuestion(q)).toContain('Choice questions need exactly one correct option.');
  });
  it('multiple select needs three options', () => {
    expect(validateQuestion({ ...base, type: 'MultipleSelect' })).toContain('Multiple-select questions need at least 3 options.');
  });
  it('true/false needs exactly two options', () => {
    const q = { ...base, type: 'TrueFalse', options: [...base.options, { text: 'Maybe', isCorrect: false, explanation: null }] };
    expect(validateQuestion(q)).toContain('True/False questions need exactly 2 options with one correct.');
  });
  it('rejects duplicate options case-insensitively', () => {
    const q = { ...base, options: [{ text: 'Yes', isCorrect: true, explanation: null }, { text: ' yes ', isCorrect: false, explanation: null }] };
    expect(validateQuestion(q)).toContain('Options must be unique.');
  });
  it('ignores options for written questions', () => {
    expect(validateQuestion({ ...base, type: 'WrittenAnswer', options: [] })).toEqual([]);
  });
  it('enforces the estimated time range', () => {
    expect(validateQuestion({ ...base, estimatedSeconds: 5 })).toContain('Estimated time must be between 10 and 3600 seconds.');
  });
});

describe('form conversions', () => {
  it('quiz minutes and percentages become API units', () => {
    const body = fromForm({ title: 'T', description: null, mode: 'Custom', timeLimitMinutes: 15, passingPercentage: 70, negativeMarkingPercent: 25,
      isPublished: true, isPremium: false, technologyId: null, difficulty: null, questionIds: ['q1'] });
    expect(body.timeLimitSeconds).toBe(900);
    expect(body.negativeMarkingFraction).toBe(0.25);
    expect(fromForm({ ...body, timeLimitMinutes: null, negativeMarkingPercent: 0 } as never).timeLimitSeconds).toBeNull();
  });
  it('prices are stored in micros without float drift', () => {
    expect(priceToMicros(9.99)).toBe(9_990_000);
    expect(priceToMicros(0.1 + 0.2)).toBe(300_000);
  });
});

describe('company content provenance', () => {
  const company: CompanyUpsert = {
    name: 'Acme', slug: null, overview: 'x', overviewSource: 'Illustrative', industry: null, logoUrl: null, websiteUrl: null, difficultyLevel: 'Intermediate',
    displayOrder: 1, isPremium: true, isActive: true, topics: [], collections: [],
    rounds: [{ name: 'Phone screen', description: 'd', source: 'Verified', sourceReference: null }],
  };
  it('verified items must cite a source', () => {
    expect(sourceProblems(company)).toHaveLength(1);
    expect(sourceProblems({ ...company, rounds: [{ ...company.rounds[0], sourceReference: 'https://careers.example.com/process' }] })).toEqual([]);
  });
  it('illustrative items need no reference', () => {
    expect(sourceProblems({ ...company, rounds: [{ ...company.rounds[0], source: 'Illustrative' }] })).toEqual([]);
  });
});

describe('role permissions', () => {
  it('maps backend roles to panel permissions', () => {
    expect(permissionsFor(['ContentManager', 'User'])).toEqual({ content: true, admin: false, superAdmin: false });
    expect(permissionsFor(['Admin'])).toEqual({ content: true, admin: true, superAdmin: false });
    expect(permissionsFor(['SuperAdmin'])).toEqual({ content: true, admin: true, superAdmin: true });
    expect(permissionsFor(['User'])).toEqual({ content: false, admin: false, superAdmin: false });
  });
});
