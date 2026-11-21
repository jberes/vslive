import { forwardRef, type ReactNode } from 'react';
import { IgrDialog, IgrButton } from 'igniteui-react';
import { ja } from '../i18n/ja';

export interface ImpactIssue { code: string; severity: string; message: string }

interface Props {
  title: string;
  children: ReactNode;
  confirmLabel: string;
  confirmDisabled: boolean;
  onConfirm: () => void;
  onClose?: () => void;
}

/** Modal used for every consequential change. Confirm is the only way to save. */
export const ImpactDialog = forwardRef<IgrDialog, Props>(function ImpactDialog(
  { title, children, confirmLabel, confirmDisabled, onConfirm, onClose }, ref,
) {
  return (
    <IgrDialog ref={ref} className="impact-dialog" onClosed={() => onClose?.()}>
      <span slot="title">{title}</span>
      <div className="impact-body">{children}</div>
      <div slot="footer" className="dialog-actions">
        <IgrButton variant="outlined" onClick={() => (ref as React.RefObject<IgrDialog | null>).current?.hide()}>{ja.common.cancel}</IgrButton>
        <IgrButton variant="contained" disabled={confirmDisabled} onClick={onConfirm}>{confirmLabel}</IgrButton>
      </div>
    </IgrDialog>
  );
});

export function IssueList({ issues }: { issues: ImpactIssue[] }) {
  if (issues.length === 0) return null;
  const blocking = issues.filter((i) => i.severity === 'Blocking');
  const warnings = issues.filter((i) => i.severity !== 'Blocking');
  return (
    <div className="issues">
      {blocking.length > 0 && (
        <section aria-label={ja.assign.issuesBlocking}>
          <h4 className="issue-head issue-blocking">✖ {ja.assign.issuesBlocking}</h4>
          <ul>{blocking.map((i) => <li key={i.code + i.message}><code>{i.code}</code> {i.message}</li>)}</ul>
        </section>
      )}
      {warnings.length > 0 && (
        <section aria-label={ja.assign.issuesWarning}>
          <h4 className="issue-head issue-warning">▲ {ja.assign.issuesWarning}</h4>
          <ul>{warnings.map((i) => <li key={i.code + i.message}><code>{i.code}</code> {i.message}</li>)}</ul>
        </section>
      )}
    </div>
  );
}
