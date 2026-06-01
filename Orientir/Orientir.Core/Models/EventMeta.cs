namespace Orientir.Core.Models;

// Метадані змагання для плашки/підписів друкованого протоколу.
// Читаються з SISTEM1.DBF (+ SISTEM1.FPT memo) теки змагання.
public class EventMeta
{
    public string EventTitle    = ""; // NAME_SOR (memo) — напр. "KYIV CITY RACE 2026"
    public string OrgName       = ""; // NAME_ORG (memo) — напр. "Клуб SEVER"
    public string HeadJudge     = ""; // GLAV_SUD — головний суддя
    public string HeadSecretary = ""; // GLAV_SEK — головний секретар
    public string Region        = ""; // REGION   — напр. "м. Київ"
    public string Period        = ""; // PERIOD   — напр. "30.05.2026 - 31.05.2026"
}
