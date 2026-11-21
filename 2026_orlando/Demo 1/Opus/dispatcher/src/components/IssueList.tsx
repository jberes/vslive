import type { AssignmentIssue } from '../api/types';
import { Icon } from './Icon';

/** Validation issues from the API dry-run: blocking first, each with shape + text, not color alone. */
export function IssueList({ issues }: { issues: AssignmentIssue[] }) {
  if (!issues.length) {
    return <p className="issue tone-ok"><Icon name="check_circle" /> No conflicts: qualified, on shift, no overlaps, SLA achievable.</p>;
  }
  const sorted = [...issues].sort((a, b) => (a.severity === b.severity ? 0 : a.severity === 'Blocking' ? -1 : 1));
  return (
    <ul className="issues">
      {sorted.map(i => (
        <li key={i.code + i.message} className={`issue tone-${i.severity === 'Blocking' ? 'crit' : 'warn'}`}>
          <Icon name={i.severity === 'Blocking' ? 'octagon' : 'warning'} />
          <span><strong>{i.severity === 'Blocking' ? 'Blocking' : 'Warning'}:</strong> {i.message}</span>
        </li>
      ))}
    </ul>
  );
}
