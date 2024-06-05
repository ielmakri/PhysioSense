"""
    Config tests.
"""

from collections import namedtuple
import os
import re

import pytest
from qtm.packet import QRTComponentType

from qlsl.config import (
    Config,
    new_lsl_stream_info,
    parse_qtm_parameters,
    qtm_packet_to_lsl_sample,
)

def save_file(data, filename):
    path = os.path.join("test", "data", filename)
    with open(path, "w") as file_content:
        file_content.write(data)

def load_file(filename):
    path = os.path.join("test", "data", filename)
    with open(path, "r") as file_content:
        data = file_content.read()
        return data

def load_config(filename):
    content = load_file(filename)
    return parse_qtm_parameters(content)

def get_channel_count(marker_count, body_count):
    return 3*marker_count + 6*body_count

def verify_config(filename, marker_count, body_count, camera_count):
    config = load_config(filename)
    channel_count = get_channel_count(marker_count, body_count)
    assert config.camera_count() == camera_count
    assert config.marker_count() == marker_count
    assert config.body_count() == body_count
    assert config.channel_count() == channel_count
    return config   

def test_qtm_stream_3d_6dof():
    verify_config(
        filename="qtm_stream_3d_6dof.xml",
        marker_count=42,
        body_count=1,
        camera_count=6,
    )

def test_qtm_stream_3d():
    verify_config(
        filename="qtm_stream_3d.xml",
        marker_count=42,
        body_count=0,
        camera_count=6,
    )

def test_qtm_stream_6dof():
    verify_config(
        filename="qtm_stream_6dof.xml",
        marker_count=0,
        body_count=1,
        camera_count=6
    )

def test_qtm_wait_6dof():
    verify_config(
        filename="qtm_wait_6dof.xml",
        marker_count=0,
        body_count=1,
        camera_count=0,
    )

def test_qtm_wait():
    verify_config(
        filename="qtm_wait.xml",
        marker_count=0,
        body_count=0,
        camera_count=0,
    )

def get_lsl_desc_xml(lines):
    res = []
    append = False
    offset = 0
    for i, line in enumerate(lines):
        line = re.sub(r"\s+", " ", line).strip()
        if line == "<desc>":
            offset = i
            append = True
        if append:
            res.append(line)
        if line == "</desc>":
            break
    assert len(res) > 0
    return res, offset

def verify_lsl_metadata(config_file, metadata_file):
    config = load_config(config_file)
    metadata = new_lsl_stream_info(config, "127.0.0.1", 50)
    actual_xml = metadata.as_xml().splitlines()
    expected_xml = load_file(metadata_file).splitlines()
    actual_xml, actual_offset = get_lsl_desc_xml(actual_xml)
    expected_xml, expected_offset = get_lsl_desc_xml(expected_xml)
    assert len(actual_xml) == len(expected_xml)
    for i, lines in enumerate(zip(actual_xml, expected_xml)):
        actual = lines[0]
        expected = lines[1]
        assert actual == expected, "Error on lines {} and {}".format(
            actual_offset + i + 1,
            expected_offset + i + 1,
        )

def save_lsl_metadata(config_file, metadata_file):
    config = load_config(config_file)
    metadata = new_lsl_stream_info(config, "127.0.0.1", 50)
    xml = metadata.as_xml()
    save_file(xml, metadata_file)

def test_lsl_metadata_3d_6dof():
    verify_lsl_metadata(
        config_file="qtm_stream_3d_6dof.xml",
        metadata_file="lsl_metadata_3d_6dof.xml",
    )

def test_lsl_metadata_3d():
    verify_lsl_metadata(
        config_file="qtm_stream_3d.xml",
        metadata_file="lsl_metadata_3d.xml",
    )

def test_lsl_metadata_6dof():
    verify_lsl_metadata(
        config_file="qtm_stream_6dof.xml",
        metadata_file="lsl_metadata_6dof.xml",
    )

class Packet:
    def __init__(self, components=[], markers=[], bodies=[]):
        self.components = components
        self.markers = markers
        self.bodies = bodies
    
    def get_3d_markers(self):
        return (self.markers, self.markers)
    
    def get_6d_euler(self):
        return (self.bodies, self.bodies)
    
    def channel_count(self):
        return get_channel_count(len(self.markers), len(self.bodies))

Position = namedtuple("Position", "x y z")
Rotation = namedtuple("Rotation", "a1 a2 a3")

def m_to_mm(m):
    return 1000*m

def verify_lsl_sample(packet):
    def verify_position(x, y, z, pos):
        assert m_to_mm(x) == pos.x
        assert m_to_mm(y) == pos.y
        assert m_to_mm(z) == pos.z
    sample = qtm_packet_to_lsl_sample(packet)
    assert len(sample) == packet.channel_count()
    n = 0
    for marker in packet.markers:
        x, y, z = sample[n], sample[n+1], sample[n+2]
        n += 3
        verify_position(x, y, z, marker)
    for position, rotation in packet.bodies:
        x, y, z = sample[n], sample[n+1], sample[n+2]
        n += 3
        verify_position(x, y, z, position)
        a1, a2, a3 = sample[n], sample[n+1], sample[n+2]
        n += 3
        assert rotation.a1 == a1
        assert rotation.a2 == a2
        assert rotation.a3 == a3
    assert n == len(sample)

def test_qtm_packet_to_lsl_3d_6dof():
    packet = Packet(
        components=[
            QRTComponentType.Component3d,
            QRTComponentType.Component6dEuler
        ],
        markers=[
            Position(31, 62, 9),
            Position(407, 808, 1209),
        ],
        bodies=[
            (Position(5, 10, 15), Rotation(30, 60, 90)),
        ],
    )
    verify_lsl_sample(packet)

def test_qtm_packet_to_lsl_3d():
    packet = Packet(
        components=[
            QRTComponentType.Component3d,
            QRTComponentType.Component6dEuler
        ],
        markers=[
            Position(31, 62, 9),
            Position(407, 808, 1209),
        ],
    )
    verify_lsl_sample(packet)

def test_qtm_packet_to_lsl_6dof():
    packet = Packet(
        components=[
            QRTComponentType.Component3d,
            QRTComponentType.Component6dEuler
        ],
        bodies=[
            (Position(5, 10, 15), Rotation(30, 60, 90)),
        ],
    )
    verify_lsl_sample(packet)

def test_qtm_packet_to_lsl_sample_empty():
    packet = Packet(
        components=[
            QRTComponentType.Component3d,
            QRTComponentType.Component6dEuler
        ],
    )
    verify_lsl_sample(packet)
