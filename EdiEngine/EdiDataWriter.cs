﻿using System.IO;
using System.Text;
using EdiEngine.Runtime;

namespace EdiEngine
{
    public class EdiDataWriter : DataWriter
    {
        private int _currentTranSegCount;
        private readonly EdiDataWriterSettings _settings;

        public EdiDataWriter(EdiDataWriterSettings settings)
        {
            _settings = settings;
        }

        public override Stream WriteToStream(EdiBatch batch)
        {
            Stream s = new MemoryStream();
            StreamWriter w = new StreamWriter(s);
            w.Write(WriteToStringBuilder(batch));
            w.Flush();
            s.Position = 0;
            return s;
        }

        public override string WriteToString(EdiBatch batch)
        {
            return WriteToStringBuilder(batch).ToString();
        }

        private StringBuilder WriteToStringBuilder(EdiBatch batch)
        {
            StringBuilder sb = new StringBuilder();

            int icn = _settings.IsaFirstControlNumber;
            int gcn = _settings.GsFirstControlNumber;

            foreach (EdiInterchange ich in batch.Interchanges)
            {
                ich.ISA = new ISA(_settings.IsaDef,
                    _settings.IsaSenderQual, _settings.IsaSenderId,
                    _settings.IsaReceiverQual, _settings.IsaReceiverId,
                    _settings.IsaEdiVersion, icn, _settings.IsaUsageIndicator);

                WriteEntity(ich.ISA, ref sb);

                foreach (EdiGroup g in ich.Groups)
                {
                    int currentTranIdx = 1;
                    g.GS = new GS(_settings.GsDef, g.FunctionalCode, _settings.GsSenderId, _settings.GsReceiverId, gcn, _settings.GsEdiVersion);
                    WriteEntity(g.GS, ref sb);

                    foreach (EdiTrans t in g.Transactions)
                    {
                        _currentTranSegCount = 0;

                        t.ST = new ST(_settings.StDef, t.Definition.EdiName, currentTranIdx);
                        WriteEntity(t.ST, ref sb);

                        foreach (MappedObjectBase ent in t.Content)
                        {
                            WriteEntity(ent, ref sb);
                        }

                        _currentTranSegCount++;
                        t.SE = new SE(_settings.SeDef, _currentTranSegCount, currentTranIdx);
                        WriteEntity(t.SE, ref sb);

                        currentTranIdx++;
                    }

                    g.GE = new GE(_settings.GeDef, g.Transactions.Count, gcn);
                    WriteEntity(g.GE, ref sb);
                    gcn++;
                }

                ich.IEA = new IEA(_settings.IeaDef, ich.Groups.Count, icn);
                WriteEntity(ich.IEA, ref sb);
                icn++;
            }

            return sb;
        }

        private void WriteEntity(MappedObjectBase ent, ref StringBuilder sb)
        {
            if (ent is EdiLoop)
            {
                foreach (var child in ((EdiLoop)ent).Content)
                {
                    WriteEntity(child, ref sb);
                }
            }
            else if (ent is EdiSegment)
            {
                _currentTranSegCount++;
                sb.Append(ent.Name);

                foreach (var el in ((EdiSegment)ent).Content)
                {
                    sb.Append($"{_settings.ElementSeparator}{el.Val}");
                }

                sb.Append(_settings.SegmentSeparator);
            }
        }
    }
}
