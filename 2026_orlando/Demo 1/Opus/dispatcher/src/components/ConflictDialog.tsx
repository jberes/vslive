import { IgrButton, IgrDialog } from 'igniteui-react';
import { useEffect, useRef } from 'react';
import type { ConflictError } from '../lib/conflict';
import { Icon } from './Icon';

/**
 * Shown when a save is refused because another dispatcher changed the job first. The dispatcher sees
 * exactly what changed and chooses explicitly: take the latest version, or re-apply on top of it.
 */
export function ConflictDialog({
  conflict, onReload, onReapply, onClose,
}: {
  conflict: ConflictError | null;
  onReload: () => void;
  onReapply?: () => void;
  onClose: () => void;
}) {
  const ref = useRef<IgrDialog>(null);
  useEffect(() => {
    if (conflict) ref.current?.show();
    else ref.current?.hide();
  }, [conflict]);

  return (
    <IgrDialog ref={ref} title="Conflicting update" onClosed={onClose} keepOpenOnEscape={false}>
      {conflict ? (
        <div className="dialog-body">
          <p className="callout tone-warn">
            <Icon name="warning" />
            <span>
              <strong>{conflict.latest.job.jobNumber}</strong> was changed
              {conflict.changedBy ? <> by <strong>{conflict.changedBy}</strong></> : null} after you opened it.
              Your change was <strong>not saved</strong>.
            </span>
          </p>
          <table className="kv-table">
            <thead>
              <tr><th scope="col">Field</th><th scope="col">You saw</th><th scope="col">Now</th></tr>
            </thead>
            <tbody>
              {conflict.changes.map(c => (
                <tr key={c.field}><th scope="row">{c.field}</th><td>{c.mine}</td><td><strong>{c.theirs}</strong></td></tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
      <IgrButton slot="footer" variant="flat" onClick={() => ref.current?.hide()}>Cancel</IgrButton>
      {onReapply ? (
        <IgrButton slot="footer" variant="outlined" onClick={onReapply}>Re-check and apply my change</IgrButton>
      ) : null}
      <IgrButton slot="footer" onClick={onReload}>Load latest version</IgrButton>
    </IgrDialog>
  );
}
