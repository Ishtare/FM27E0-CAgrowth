// GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fm24Editor
{
    sealed class ClubFinanceCalibration
    {
        enum Shape { Unknown, Compact, Legacy, Current }

        static readonly byte[] FullName = Encoding.ASCII.GetBytes(".?AVCLUB_FINANCE@db@@\0");
        static readonly byte[] CompactName = Encoding.ASCII.GetBytes(".?AVCLUB_FINANCE_BASE@db@@\0");
        static readonly byte[] TransferRead = { 0x8B, 0x81, 0xC4, 0x07, 0, 0 };
        static readonly byte[] WageGetter = { 0x8B, 0x81, 0x08, 0x08, 0, 0, 0xC3 };
        static readonly byte[] CorporateGetter = { 0x0F, 0xB6, 0x81, 0xAD, 0x08, 0, 0, 0xC3 };
        internal const ulong CorporateOffset = 0x8AD, TicketOffset = 0x10;

        readonly Func<ulong,int,byte[]> read;
        readonly ulong module, size;
        readonly Dictionary<ulong,Shape> shapes = new Dictionary<ulong,Shape>();
        internal readonly List<ulong> FullVotes = new List<ulong>();
        internal readonly List<ulong> CompactVotes = new List<ulong>();

        public ClubFinanceCalibration(Func<ulong,int,byte[]> read, ulong module, ulong size)
        {
            this.read = read;
            this.module = module;
            this.size = size;
        }

        bool InImage(ulong address, int length)
        {
            return address >= module && (ulong)length <= size && address - module <= size - (ulong)length;
        }

        ulong Pointer(ulong address)
        {
            var bytes = read(address, 8);
            return bytes != null && bytes.Length == 8 ? BitConverter.ToUInt64(bytes, 0) : 0;
        }

        bool MatchesBytes(ulong address, byte[] expected)
        {
            if (!InImage(address, expected.Length)) return false;
            var bytes = read(address, expected.Length);
            return bytes != null && bytes.SequenceEqual(expected);
        }

        ulong TypeNameAddress(ulong table)
        {
            if (module < 0x10000 || table <= module || (table & 7) != 0 || !InImage(table - 8, 16)) return 0;
            ulong locator = Pointer(table - 8);
            if (!InImage(locator, 24)) return 0;
            var bytes = read(locator, 24);
            if (bytes == null || bytes.Length != 24 || BitConverter.ToUInt32(bytes, 0) != 1 ||
                BitConverter.ToUInt32(bytes, 4) != 0 || BitConverter.ToUInt32(bytes, 20) != locator - module) return 0;
            uint descriptor = BitConverter.ToUInt32(bytes, 12);
            return descriptor != 0 && (ulong)descriptor < size ? module + descriptor + 16 : 0;
        }

        bool MethodMatches(ulong table, ulong slot, byte[] proof)
        {
            ulong entry = table + slot;
            return InImage(entry, 8) && MatchesBytes(Pointer(entry), proof);
        }

        bool ReadsTransferBudget(ulong table)
        {
            const int window = 96;
            ulong entry = table + 0x98;
            if (!InImage(entry, 8)) return false;
            ulong method = Pointer(entry);
            if (!InImage(method, window)) return false;
            var bytes = read(method, window);
            if (bytes == null || bytes.Length != window) return false;
            // The transfer-budget routine has a prologue before the field load.
            for (int i = 0; i <= bytes.Length - TransferRead.Length; i++)
                if (bytes[i] == TransferRead[0] && bytes.Skip(i).Take(TransferRead.Length).SequenceEqual(TransferRead))
                    return true;
            return false;
        }

        Shape Recognize(ulong table)
        {
            Shape shape;
            if (shapes.TryGetValue(table, out shape)) return shape;
            ulong name = TypeNameAddress(table);
            shape = Shape.Unknown;
            if (MatchesBytes(name, FullName))
            {
                // The 27.1 finance class exposes these native getters through its vtable.
                // The object may legitimately have unset facilities, so use code to tell the layouts apart.
                if (!MethodMatches(table, 0x498, CorporateGetter))
                    shape = Shape.Legacy;
                else if (FmLayout.ClubFinance == 0x150 && FmLayout.ClubData == 0x100 && FmLayout.DataSeasonTickets == 0xC0 &&
                    ReadsTransferBudget(table) && MethodMatches(table, 0x148, WageGetter))
                    shape = Shape.Current;
            }
            else if (MatchesBytes(name, CompactName)) shape = Shape.Compact;
            shapes.Add(table, shape);
            return shape;
        }

        public void Observe(ulong finance)
        {
            if (finance <= 0x10000 || finance >= 0x800000000000UL || (finance & 7) != 0) return;
            ulong table = Pointer(finance);
            var shape = Recognize(table);
            if (shape == Shape.Compact) CompactVotes.Add(table);
            else if (shape == Shape.Current || (shape == Shape.Legacy && FmLayout.FacilityFin.Any(o => o != 0) &&
                FmLayout.FacilityFin.All(o =>
                {
                    if (o == 0) return true;
                    var value = read(finance + o, 1);
                    return value != null && value.Length == 1 && value[0] >= 1 && value[0] <= 20;
                })))
                FullVotes.Add(table);
        }

        public bool CurrentFields(ulong table) { return Recognize(table) == Shape.Current; }
        public bool IsCompact(ulong table) { return Recognize(table) == Shape.Compact; }

        public static bool CheckedTickets(FmProcess fm)
        {
            return fm.VerifiedCurrentBuild || FmLayout.FinTicketPrice == TicketOffset &&
                new ClubFinanceCalibration(fm.Read, fm.Base, fm.ImageSize).CurrentFields(fm.Base + FmProcess.FinanceVtRva);
        }
    }
}
