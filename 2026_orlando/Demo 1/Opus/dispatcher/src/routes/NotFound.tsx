import { Link } from 'react-router-dom';
import { EmptyState } from '../components/States';

export default function NotFound() {
  return (
    <div className="page-pad">
      <EmptyState icon="warning" title="Page not found">
        <Link to="/ledger">Return to the operations ledger</Link>
      </EmptyState>
    </div>
  );
}
