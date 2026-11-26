import hashlib
import io
import json
from pathlib import Path
import struct
import tempfile
import unittest
import uuid
from unittest.mock import patch
import cutover_tool as tool

def string(value):
    raw=value.encode();length=len(raw);header=bytearray()
    while length>=128: header.append((length&127)|128);length>>=7
    return bytes(header)+bytes([length])+raw

class CutoverTests(unittest.TestCase):
    def fixture(self,root,pending=False,native_pending=False):
        backend=root/'backend'/'epoch'/'sources';native=root/'native';backend.mkdir(parents=True);native.mkdir()
        for owner in (backend,native): (owner/'owner.lock').write_bytes(b'')
        (backend.parent/'ownership.protocol').write_text('2:source-until-handler-proof')
        (native/'namespace.sha256').write_bytes(b'n'*32)
        subject=struct.pack('<i',7)+string('TickAggregationRealtime')+string('Trade')+string('ES20261218')
        header=subject+uuid.uuid4().bytes_le+string('GLBX.MDP3')+uuid.uuid4().bytes_le
        key=tool.sha(header);source=tool.sha(header+b'unknown'+b'original');manifest=tool.sha(subject)
        body=struct.pack('<i',2)+string(key)+string(source)+string(manifest)+header+string('')+string('')+b'\0'+struct.pack('<i',1)+subject+struct.pack('<i',8)+b'original'
        frame=struct.pack('<I',len(body))+hashlib.sha256(body).digest()+body+b'\0'*4
        (backend/'0001.rto').write_bytes(frame)
        row=bytes.fromhex(key+source)+uuid.uuid4().bytes_le+bytes.fromhex(manifest)+struct.pack('<i',1)
        (backend/'receipts.bin').write_bytes(b'' if pending else row+hashlib.sha256(row).digest())
        (backend/'delivery-receipts.bin').write_bytes(b'')
        fields=[b'scope.source-capture.Futures',b'',b'original-native']
        packet=struct.pack('<Q',1)+b'k'*32+b''.join(struct.pack('<I',len(value))+value for value in fields)
        (native/'0001.pubwal').write_bytes(b'IFMSRC02'+struct.pack('<I',len(packet))+packet+hashlib.sha256(packet).digest())
        ordinal=struct.pack('<Q',1)
        (native/'confirmed.bin').write_bytes(b'' if native_pending else ordinal+hashlib.sha256(ordinal).digest())
        config=root/'config'/'base.json';config.parent.mkdir();config.write_bytes(b'{"Actors":{"RealtimeMailboxes":{"Enabled":false,"Epoch":"original"}}}\n')
        return backend,native,config
    def test_drained_rehearsal_switches_and_rolls_back_exact_bytes_without_touching_originals(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);backend,native,config=self.fixture(root);original=config.read_bytes()
            result=tool.rehearse(backend,native,config,root/'rehearsal','next-epoch')
            self.assertEqual('Passed',result['state']);self.assertFalse(result['activationPerformed']);self.assertFalse(result['liveCutoverQualified'])
            self.assertEqual(original,config.read_bytes());self.assertEqual(original,(root/'rehearsal'/'appsettings.rehearsal.json').read_bytes())
            candidate=json.loads((root/'rehearsal'/'candidate.review-only.json').read_bytes())
            self.assertTrue(candidate['Actors']['RealtimeMailboxes']['Enabled']);self.assertFalse(candidate['Actors']['SessionCapture']['Enabled'])
            self.assertEqual('next-epoch',candidate['Actors']['RealtimeMailboxes']['Epoch'])
    def test_pending_backend_or_native_ownership_refuses_before_output(self):
        for backend_pending,native_pending in ((True,False),(False,True)):
            with self.subTest(backend=backend_pending,native=native_pending),tempfile.TemporaryDirectory() as temporary:
                root=Path(temporary);backend,native,config=self.fixture(root,backend_pending,native_pending)
                with self.assertRaisesRegex(ValueError,'Pending'):tool.rehearse(backend,native,config,root/'output','next')
                self.assertFalse((root/'output').exists())
    def test_torn_checksum_and_uncommitted_source_refuse_without_repairing_original(self):
        for fault in ('torn','checksum','uncommitted'):
            with self.subTest(fault=fault),tempfile.TemporaryDirectory() as temporary:
                root=Path(temporary);backend,native,config=self.fixture(root);path=backend/'0001.rto';raw=path.read_bytes()
                changed=raw[:-1] if fault=='torn' else raw[:-4] if fault=='uncommitted' else raw[:20]+bytes([raw[20]^1])+raw[21:]
                path.write_bytes(changed)
                with self.assertRaises(ValueError):tool.rehearse(backend,native,config,root/'output','next')
                self.assertEqual(changed,path.read_bytes())
    def test_wrong_protocol_namespace_and_epoch_are_refused(self):
        for fault in ('archive','namespace','epoch'):
            with self.subTest(fault=fault),tempfile.TemporaryDirectory() as temporary:
                root=Path(temporary);backend,native,config=self.fixture(root)
                if fault=='archive':(backend.parent/'archive-only.protocol').write_bytes(b'archive')
                if fault=='namespace':(native/'namespace.sha256').write_bytes(b'wrong')
                with self.assertRaises(ValueError):tool.rehearse(backend,native,config,root/'output','original' if fault=='epoch' else 'next')
    def test_switch_readback_failure_still_restores_original(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);backend,native,config=self.fixture(root);original=config.read_bytes();real=tool.atomic
            def corrupt(path,content):
                real(path,content)
                if path.name=='appsettings.rehearsal.json' and content!=original:path.write_bytes(b'corrupt')
            with patch.object(tool,'atomic',side_effect=corrupt),self.assertRaisesRegex(ValueError,'readback'):tool.rehearse(backend,native,config,root/'output','next')
            self.assertEqual(original,(root/'output'/'appsettings.rehearsal.json').read_bytes())
    def test_active_owner_lease_refuses(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);backend,native,config=self.fixture(root)
            with tool.stopped_owner(backend),self.assertRaises(ValueError):tool.rehearse(backend,native,config,root/'output','next')
    def test_existing_or_overlapping_output_refused(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);backend,native,config=self.fixture(root)
            for output in (backend/'nested',config.parent/'nested',root):
                with self.assertRaises(ValueError):tool.rehearse(backend,native,config,output,'next')
    def test_native_control_uses_exact_lease_and_validates_readback_without_claiming_backend_drain(self):
        replies=[{'kind':'NativeForwardingOnly','crossOwnerSnapshot':False,'status':{'supported':True,'lease':0,'activeSends':0}},
                 {},{'kind':'NativeForwardingOnly','crossOwnerSnapshot':False,'status':{'supported':True,'lease':123,'activeSends':0}}]
        with patch.object(tool.urllib.request,'urlopen',side_effect=[io.BytesIO(json.dumps(value).encode()) for value in replies]) as send:
            result=tool.native_forwarding('http://127.0.0.1:8099','s'*32,123,True)
            self.assertTrue(result['held']);self.assertFalse(result['backendDrained']);self.assertFalse(result['activationPerformed'])
            self.assertTrue(send.call_args_list[1].args[0].full_url.endswith('/hold/7b'))
    def test_native_control_rejects_unowned_lease_and_remote_endpoint_before_post(self):
        with patch.object(tool.urllib.request,'urlopen') as send:
            with self.assertRaises(ValueError):tool.native_forwarding('http://192.0.2.1:80','s'*32,1,True)
            send.assert_not_called()
        reply={'kind':'NativeForwardingOnly','crossOwnerSnapshot':False,'status':{'supported':True,'lease':999,'activeSends':0}}
        with patch.object(tool.urllib.request,'urlopen',return_value=io.BytesIO(json.dumps(reply).encode())) as send:
            with self.assertRaisesRegex(ValueError,'Another owner'):tool.native_forwarding('http://localhost:8099','s'*32,123,False)
            self.assertEqual(1,send.call_count)
if __name__=='__main__':unittest.main()
