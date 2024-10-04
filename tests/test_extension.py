# -*- coding: utf-8 -*-

"""Test CLR extension method support."""

import Python.Test as Test
import pytest

def test_linq_extensions():
    """Test LINQ extensions."""
    import clr
    from System import String, Func
    from System.Collections.Generic import List
    from System import Linq
    clr.ImportExtensions(Linq)
    list = List[String]()
    list.Add('hello')
    list.Add('beautiful')
    list.Add('world')
    assert list.First[String]() == 'hello'
    assert list.Skip[String](1).First[String]() == 'beautiful'
    list.FirstOrDefault[String](Func[String, bool](lambda x: x.startswith('w'))) == 'world'
