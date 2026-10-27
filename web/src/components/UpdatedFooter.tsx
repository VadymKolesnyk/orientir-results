interface Props {
  // Будь-які рядки з updated_at: результати учасників або команди естафети.
  results: { updated_at: string }[]
  offline: boolean
}

export function UpdatedFooter({ results, offline }: Props) {
  const last = results.reduce(
    (m, r) => (r.updated_at > m ? r.updated_at : m),
    '',
  )
  let text: string
  if (offline) {
    const at = last
      ? ' · останнє: ' + new Date(last).toLocaleTimeString('uk-UA')
      : ''
    text = `Офлайн — натисніть «офлайн · оновити» вгорі, щоб перепідключитись${at}`
  } else {
    text = last
      ? 'Оновлено: ' + new Date(last).toLocaleTimeString('uk-UA')
      : ''
  }
  return (
    <div className="upd" id="upd">
      {text}
    </div>
  )
}
