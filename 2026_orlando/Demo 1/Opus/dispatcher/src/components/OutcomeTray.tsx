import { IgrIconButton } from 'igniteui-react';
import { useSyncExternalStore } from 'react';
import { Icon } from './Icon';
import { hms } from '../lib/time';
import { outcomes } from '../state/outcomeStore';
import { useSession } from '../state/session';

const ICON = { ok: 'check_circle', crit: 'octagon', warn: 'warning', info: 'info', neutral: 'info' } as const;

export function OutcomeTray() {
  const list = useSyncExternalStore(outcomes.subscribe, outcomes.get);
  const { opsOffset } = useSession();
  return (
    <div className="outcome-tray" aria-live="assertive" aria-relevant="additions">
      {list.map(o => (
        <div key={o.id} className={`outcome tone-${o.tone}`} role={o.tone === 'crit' ? 'alert' : 'status'}>
          <Icon name={ICON[o.tone]} />
          <div className="outcome-body">
            <strong>{o.title}</strong>
            {o.detail ? <span>{o.detail}</span> : null}
            <span className="muted small">{hms(o.at + opsOffset)} EST</span>
          </div>
          <IgrIconButton variant="flat" aria-label="Dismiss message" onClick={() => outcomes.dismiss(o.id)}>
            <Icon name="close" />
          </IgrIconButton>
        </div>
      ))}
    </div>
  );
}
