package main

import (
	"encoding/hex"
	"testing"
)

/*
var a []int

func init() {
	for i := 0; i < 1000000; i++ {
		a = append(a, i)
	}
}
func BenchmarkMergeSortMulti(b *testing.B) {
	for n := 0; n < b.N; n++ {
		MergeSortMulti(a)
	}
}

func BenchmarkMergeSort(b *testing.B) {
	for n := 0; n < b.N; n++ {
		MergeSort(a)
	}
}
*/

var encodingTestCases = [...]struct {
	win1250, utf string
}{
	// https://en.wikipedia.org/wiki/Windows-1250
	{"", ""},
	{" ", " "},
	{"a", "a"},
	{"a\nb c123!#", "a\nb c123!#"},
	{"\xe8", "č"},
	{"abc \xe8\x9e\x9a\xe6\xf0---\xc8\x8e\x8a\xc6\xd0", "abc čžšćđ---ČŽŠĆĐ"},
}

/*
func TestMain(m *testing.M) {
	Init()
	retCode := m.Run()
	//myTeardownFunction()
	os.Exit(retCode)
}
*/

func TestDecodeWindows1250(t *testing.T) {
	for _, table := range encodingTestCases {
		result := DecodeWindows1250(table.win1250)
		if result != table.utf {
			t.Errorf("Decoding %s: %s gave: %s: %s instead of expected %s: %s", table.win1250, hex.Dump([]byte(table.win1250)), result, hex.Dump([]byte(result)), table.utf, hex.Dump([]byte(table.utf)))
		}
	}
}

func TestEncodeWindows1250(t *testing.T) {
	for _, table := range encodingTestCases {
		result := EncodeWindows1250(table.utf)
		if result != table.win1250 {
			t.Errorf("Encoding %s (%v) gave: %s (%v), instead of expected: %s (%v).", table.utf, hex.Dump([]byte(table.utf)), result, hex.Dump([]byte(result)), table.win1250, hex.Dump([]byte(table.win1250)))
		}
	}
}

func TestApplyTagLanguagePostfix(t *testing.T) {
	assertEqual(t, ApplyTagLanguagePostfix("", 0), ":it")
	assertEqual(t, ApplyTagLanguagePostfix("a", 20), "a:hu")
	assertEqual(t, ApplyTagLanguagePostfix("ŠomethingUTF", 14.9), "ŠomethingUTF:hu")
	assertEqual(t, ApplyTagLanguagePostfix("realistic:tag", 14.2), "realistic:tag:it")

}

func BenchmarkApplyTagLanguagePostfix(b *testing.B) {
	b.ReportAllocs()
	for n := 0; n < b.N; n++ {
		ApplyTagLanguagePostfix("something", 14.1)
	}
}

func TestNormalizeHouseNumbers(t *testing.T) {
	//assertEqual(t, NormalizeHouseNumber(""), "000_")
	assertEqual(t, NormalizeHouseNumber("a"), "000a")
	assertEqual(t, NormalizeHouseNumber("1"), "001_")
	assertEqual(t, NormalizeHouseNumber("12"), "012_")
	assertEqual(t, NormalizeHouseNumber("2b"), "002b")
	assertEqual(t, NormalizeHouseNumber("123c"), "123c")
	assertEqual(t, NormalizeHouseNumber("123ž"), "123ž")
}

func BenchmarkNormalizeHouseNumbersWithoutLetter(b *testing.B) {
	b.ReportAllocs()
	for n := 0; n < b.N; n++ {
		NormalizeHouseNumber("12")
	}
}
func BenchmarkNormalizeHouseNumbersWithLetter(b *testing.B) {
	b.ReportAllocs()
	for n := 0; n < b.N; n++ {
		NormalizeHouseNumber("123ž")
	}
}

// utility methods
func assertEqual(t *testing.T, testedValue, expected interface{}) {
	if testedValue != expected {
		t.Errorf("\"%s\" != \"%s\"", testedValue, expected)
	}
}

func assertBetween(t *testing.T, testedValue, lowBound, upperBound int) {
	if testedValue <= lowBound || testedValue >= upperBound {
		t.Errorf("%d should be between %d and %d", testedValue, lowBound, upperBound)
	}
}

/*
func TestAll(t *testing.T) {
	ReadLookups()
	ProcessOne("data/temp/HS-epsg4326/HS-epsg4326.shp")
}

func BenchmarkAll(b *testing.B) {
	b.ReportAllocs()
	ReadLookups()
	for n := 0; n < b.N; n++ {
		ProcessOne("data/temp/HS-epsg4326/HS-epsg4326.shp")
	}
}
*/
func BenchmarkReadShapefile(b *testing.B) {
	if testing.Short() {
		b.Skip("skipping test in short mode.")
	}
	b.ReportAllocs()
	ReadLookups()
	b.ResetTimer()
	_ = ReadShapefile("data/temp/HS-epsg4326/HS-epsg4326.shp")
}

func BenchmarkSort(b *testing.B) {
	if testing.Short() {
		b.Skip("skipping test in short mode.")
	}
	b.ReportAllocs()
	ReadLookups()
	featureCollections := ReadShapefile("data/temp/HS-epsg4326/HS-epsg4326.shp")

	b.ResetTimer()
	for _, c := range featureCollections {
		SortFeatureCollection(*c)
	}
}

func BenchmarkReadLookups(b *testing.B) {
	if testing.Short() {
		b.Skip("skipping test in short mode.")
	}
	b.ReportAllocs()
	ReadLookups()
	b.ResetTimer()

	ReadLookups()
}

func TestReadLookups(t *testing.T) {
	if testing.Short() {
		t.Skip("skipping test in short mode.")
	}
	ReadLookups()

	// just check the length ranges
	assertBetween(t, len(ptCodeMap), 400, 500)
	assertBetween(t, len(ptNameMap), 400, 500)
	assertBetween(t, len(ulNameMap), 10000, 11000)
	assertBetween(t, len(ulNameDjMap), 600, 700)
	assertBetween(t, len(naNameMap), 6000, 7000)
	assertBetween(t, len(naNameDjMap), 50, 60)
	assertBetween(t, len(obNameMap), 210, 220)
}
