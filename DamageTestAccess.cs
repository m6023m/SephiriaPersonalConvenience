using System.Runtime.CompilerServices;

// The isolated verifier is compiled as a separate assembly by
// run-damage-tooltip-smoke.ps1. Keep calculation helpers internal to the mod.
[assembly: InternalsVisibleTo("DamageTooltipSmoke")]
