namespace Akki_ARIA_Importer_MG
{
    internal sealed class StoreSummary
    {
        public int Accepted { get; private set; }
        public int Warnings { get; private set; }
        public int Failed { get; private set; }
        public int Unconfirmed { get; private set; }

        public void Record(int? status)
        {
            if (!status.HasValue) Unconfirmed++;
            else if (status == 0) Accepted++;
            else if ((status & 0xF000) == 0xB000) Warnings++;
            else Failed++;
        }

        public override string ToString()
        {
            return string.Format("C-STORE replies: {0} accepted, {1} warnings, {2} failed, {3} unconfirmed.",
                Accepted, Warnings, Failed, Unconfirmed);
        }
    }
}
