using System;
using System.IO;
using UnityEngine;

namespace SephiriaRoomRetry
{
    // The native run spending field survives room reloads and is settled exactly once by the game.
    internal sealed class RetryPayment
    {
        internal const string CountKey = "PersonalConvenience_DeathRoomRetries";
        internal SaveData Run;
        internal string PathName;
        internal string SpendingKey;
        internal int Cost;
        internal long Available;
        private byte[] original;
        private bool committed;

        internal static RetryPayment Read(string slot, int playerIndex)
        {
            var result = new RetryPayment();
            result.PathName = Path.Combine(SaveData.CommonPath, slot + "TMP.sav");
            result.original = File.ReadAllBytes(result.PathName);
            result.Run = new SaveData(true, ".sav", 1);
            var profile = new SaveData(true);
            if (!result.Run.LoadFromString(File.ReadAllText(result.PathName)) ||
                !profile.LoadFromString(File.ReadAllText(Path.Combine(SaveData.CommonPath, slot + ".sav"))) ||
                result.Run.GetInt("SaveVersion", 0) <= 0)
                throw new InvalidDataException("Invalid retry payment checkpoint");
            string prefix = "Player" + playerIndex;
            result.SpendingKey = prefix + "SapphireUseInRun";
            int retries = Math.Max(0, result.Run.GetInt(CountKey, 0));
            result.Cost = 2 << Math.Min(4, retries);
            result.Available = (long)profile.GetInt("Sapphire", 0) + result.Run.GetInt(prefix + "SapphireInRun", 0)
                - result.Run.GetInt(result.SpendingKey, 0);
            return result;
        }
        internal void Commit()
        {
            if (Available < Cost) throw new InvalidOperationException("Not enough checkpoint sapphires");
            Run.SetInt(SpendingKey, checked(Run.GetInt(SpendingKey, 0) + Cost));
            Run.SetInt(CountKey, Math.Min(4, Math.Max(0, Run.GetInt(CountKey, 0))) + 1);
            string temporary = PathName + ".retry-payment-" + Guid.NewGuid().ToString("N");
            try
            {
                Run.version = Application.version;
                Run.enableCloudSave = false;
                Run.Save(temporary);
                var verified = new SaveData(true, ".sav", 1);
                if (!File.Exists(temporary) || !verified.LoadFromString(File.ReadAllText(temporary)) ||
                    verified.GetInt(SpendingKey, -1) != Run.GetInt(SpendingKey, 0) ||
                    verified.GetInt(CountKey, -1) != Run.GetInt(CountKey, 0))
                    throw new IOException("Retry payment save failed");
                File.Replace(temporary, PathName, null);
                committed = true;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        internal void Refund()
        {
            if (!committed) return;
            string temporary = PathName + ".retry-refund-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllBytes(temporary, original);
                File.Replace(temporary, PathName, null);
                committed = false;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
