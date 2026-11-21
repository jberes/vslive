import { Link, useParams } from 'react-router-dom';
import { Icon } from '../../components/Icon';
import { JobPanel } from '../../components/JobPanel';
import { ledgerHref, ledgerHrefFor } from '../../state/ledgerState';

/** Full job record. "Back" returns to the ledger with the same filters and this job selected. */
export default function JobPage() {
  const id = Number(useParams().id);
  return (
    <div className="job-page">
      <nav className="back-row" aria-label="Breadcrumb">
        <Link to={ledgerHrefFor(id)} className="link-btn"><Icon name="arrow_back" /> Back to ledger (filters kept)</Link>
        <span className="muted small">Ledger filters: {decodeURIComponent(ledgerHref().split('?')[1] ?? 'none').replace(/&/g, ' · ')}</span>
      </nav>
      {Number.isFinite(id) ? <JobPanel jobId={id} full /> : <p>Invalid job id.</p>}
    </div>
  );
}
