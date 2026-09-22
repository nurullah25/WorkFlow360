/** countLabel(1, 'task') -> "1 task", countLabel(3, 'task') -> "3 tasks", countLabel(2, 'person', 'people') -> "2 people" */
export function countLabel(count: number, singular: string, plural = `${singular}s`): string {
  return `${count} ${count === 1 ? singular : plural}`;
}
