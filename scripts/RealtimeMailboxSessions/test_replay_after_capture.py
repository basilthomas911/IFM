import json,tempfile,unittest
from pathlib import Path
import replay_after_capture as replay
import session_tool as tools
class ReplayOrchestrationTests(unittest.TestCase):
    def test_relocation_uses_unique_root_identity_and_longest_authoritative_root(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);original=root/'original';sub=original/'nested';out=root/'restored'
            values={'Actors:RealtimeMailboxes:Directory':str(sub/'journal'),'TradeBroker:Emulator:LedgerPath':str(original/'ledger.json')}
            rows=[{'owner':'same','restoreOwner':'same-0','path':str(original)},{'owner':'same','restoreOwner':'same-1','path':str(sub)}]
            moved=replay.relocate(values,rows,out)
            self.assertEqual(str(out/'owners'/'same-1'/'journal'),moved['Actors:RealtimeMailboxes:Directory'])
            self.assertEqual(str(out/'owners'/'same-0'/'ledger.json'),moved['TradeBroker:Emulator:LedgerPath'])
    def test_uncovered_original_owner_is_not_silently_replaced(self):
        with tempfile.TemporaryDirectory() as d:
            with self.assertRaises(ValueError):replay.relocate({'RawFuturesStorage:Directory':d},[],Path(d)/'restored')
    def test_native_raw_special_mapping_preserves_explicit_restore_relative_path(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);source=root/'original';mapped=replay.relocate({'RawFuturesStorage:Directory':str(source)},[{'owner':'native-checkpoint','path':str(source),'restoreRelativePath':'prefixes/raw'}],root/'restored')
            self.assertEqual(str(root/'restored/owners/native-checkpoint/prefixes/raw'),mapped['RawFuturesStorage:Directory'])
    def test_frozen_tree_ignores_appsettings_and_retains_all_loaded_binary_hashes(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);api=root/'api';runner=root/'runner';native=root/'native'
            for p in (api,runner,native):p.mkdir()
            for p in (api/'actual.dll',api/'dependency.dll',runner/'runner.dll',native/'native.dll'):p.write_bytes(p.name.encode())
            (api/'actual.runtimeconfig.json').write_text('{}');(api/'appsettings.json').write_text('secret')
            calendar=root/'calendar.json';calendar.write_text('{}')
            pin=lambda p:{'path':str(p),'sha256':tools.digest(p)}
            cfg={'apiAssembly':pin(api/'actual.dll'),'benchmarkAssembly':pin(runner/'runner.dll'),'nativeLibrary':pin(native/'native.dll'),'calendarEvidence':pin(calendar)}
            config=root/'config.json';tools.write(config,cfg)
            evidence=root/'configuration.json';tools.write(evidence,{'RuntimeAssemblies':[{'RelativePath':p.name,'Bytes':p.stat().st_size,'Sha256':tools.digest(p),'ModuleVersionId':'12345678-1234-1234-1234-123456789012'}for p in (api/'actual.dll',api/'dependency.dll')]})
            proof=replay.freeze_runtime(evidence,config,root/'frozen');state={'frozenRuntime':proof,'runtimeSettings':pin(evidence)}
            entries=replay.runtime_entries(state);self.assertIn('calendar',entries);self.assertFalse((root/'frozen/api/appsettings.json').exists())
            (root/'frozen/api/dependency.dll').write_bytes(b'changed')
            with self.assertRaises(ValueError):replay.runtime_entries(state)
    def test_regenerable_cleanup_retains_financial_receipt_and_original_sources(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);mode=root/'replays/Memory';working=mode/'restored/working-archive';working.mkdir(parents=True);(working/'body').write_text('derived')
            ledger=mode/'restored/owners/emulator/ledger.json';ledger.parent.mkdir(parents=True);ledger.write_text('authority')
            receipts=mode/'restored/owners/real-mailbox/epoch/sources/receipts.bin';receipts.parent.mkdir(parents=True);receipts.write_text('authority')
            original=root/'original-source';original.mkdir();(original/'body').write_text('original')
            replay.cleanup_regenerable(mode,{'initialSnapshot':{'sha256':'a'},'sourceArchive':{'sha256':'b'}})
            self.assertFalse(working.exists());self.assertTrue(ledger.exists());self.assertTrue(receipts.exists());self.assertTrue((original/'body').exists())
    def test_missing_complete_two_session_evidence_fails_before_any_fixture_creation(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);state=root/'state.json';cfg=root/'config.json';tools.write(state,{'sessions':[]});tools.write(cfg,{'version':1})
            with self.assertRaises(ValueError):replay.validate(state,cfg)
if __name__=='__main__':unittest.main()
