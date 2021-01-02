solution("xconsole")
	language("C#")
	location("")
	warnings("Extra")

	configurations({"Debug", "Release"})

	filter("configurations:Debug")
		defines("DEBUG")
		symbols("On")
		objdir("obj/debug")
		targetdir("bin/debug")

	filter("configurations:Release")
		optimize("On")
		objdir("obj/release")
		targetdir("bin/release")

	project("xconsole")
		kind("WindowedApp")
		defines("TRACE")
		files("source/**.cs")
		links({"System", "System.Drawing", "System.Windows.Forms"})
