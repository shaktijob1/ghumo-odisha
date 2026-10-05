namespace GhumoOdisha.Infrastructure.Persistence.Seed;

/// <summary>
/// The first two travel stories, inserted once by the BlogAndTravelMoments migration. After that
/// they are ordinary stories: the admin edits them, adds hero photos or deletes them from the
/// Stories page, and a deleted one does not come back.
/// </summary>
public static class BlogSeed
{
    public static readonly DateTime PublishedAt = new(2026, 10, 5, 6, 0, 0, DateTimeKind.Utc);

    public sealed record Story(string Title, string Slug, string Place, string Excerpt, string Content, string[] Tags);

    public static readonly Story Koraput = new(
        "Best Places to Visit in Koraput: A Complete Travel Guide",
        "best-places-to-visit-in-koraput",
        "Koraput",
        "Deomali peak, Duduma waterfall, Gupteswar cave, coffee estates and tribal culture — the best places to visit in Koraput, the best time to go and how to plan your trip from Bhubaneswar.",
        """
        Koraput is the green heart of southern Odisha. Spread across the Eastern Ghats, it is a land of misty hills, roaring waterfalls, coffee plantations, ancient cave temples and some of India's oldest tribal communities. Whether you come for a weekend of sightseeing or a slow trip through the villages, Koraput rewards every traveller.

        This guide covers the best places to visit in Koraput, the best time to go, how many days you need and how to reach Koraput from Bhubaneswar.

        ## 1. Deomali – the highest peak of Odisha
        At about 1,672 metres, Deomali is the highest point in Odisha. A short trek from the road brings you to grassy meadows and rocky viewpoints with endless rolling hills all around. Come early for sunrise: on winter mornings the valleys below fill with clouds and the view is unforgettable.

        ## 2. Duduma Waterfall
        Duduma is one of the tallest waterfalls in Odisha, where the Machkund river drops into a deep gorge on the Odisha – Andhra Pradesh border. It is at its most powerful just after the monsoon, from August to November. Nearby Rani Duduma is a smaller, quieter fall that is just as pretty.

        ## 3. Gupteswar Cave Temple
        Hidden in a forest on the banks of the Kolab river, Gupteswar is a limestone cave with a natural Shiva lingam, often called the "Gupta Kedar" of Odisha. Climbing the stone steps through the trees to the cave is part of the experience, and the temple is crowded with pilgrims during Shravan and Maha Shivaratri.

        ## 4. Kolab Dam and reservoir
        The Upper Kolab reservoir near Jeypore is a wide sheet of blue water ringed by hills. It is a calm spot for photographs, a picnic and a sunset, and the drive there passes through pretty farmland and villages.

        ## 5. Sabara Srikhetra – Jagannath Temple, Koraput
        Built on a hill in Koraput town, Sabara Srikhetra is a Jagannath temple that celebrates the deity's tribal roots. Its walls carry images of deities from across India, and the hilltop gives a lovely view of the town, especially in the evening.

        ## 6. Tribal Museum, Koraput
        To understand Koraput, visit the tribal museum run by the Council of Analysis of Tribal Art and Culture (COATS). It displays the costumes, jewellery, musical instruments, hunting tools and daily life of the region's tribal communities.

        ## 7. Coffee plantations
        Koraput grows its own coffee in the cool hills around the town. A walk through the estates, with shade trees, coffee bushes and the smell of roasting beans, is one of the most relaxing things to do here. Do take a pack of Koraput coffee home.

        ## 8. Jeypore and the surrounding villages
        Jeypore, the old royal town of the region, is a good base for Kolab and Gupteswar. Around it lie weekly tribal haats (markets), terraced fields and villages where life still follows the seasons. Visit respectfully and always ask before taking photographs of people.

        ## 9. Kotpad weaving village
        Kotpad is known for its handloom saris and stoles, coloured with natural dyes from the roots of the aal tree. Watching the weavers at work, and buying straight from them, is a memorable way to support local craft.

        ## Best time to visit Koraput
        October to February is the best time to visit Koraput: the weather is cool and clear, ideal for Deomali sunrises and sightseeing. July to September brings full waterfalls and very green hills, but some roads can be slippery. March to June is warmer, though the hills stay pleasant in the mornings and evenings.

        ## How many days do you need?
        Plan three to four days for Koraput. That gives you a day for Deomali and the hills, a day for Duduma, a day for Jeypore, Kolab and Gupteswar, and time for the coffee estates and the tribal museum.

        ## How to reach Koraput from Bhubaneswar
        Koraput is roughly 500 km from Bhubaneswar by road, a long but scenic drive through the Eastern Ghats. Koraput also has a railway station with direct trains from Bhubaneswar. The nearest big airport is Visakhapatnam.

        ## Travel tips
        - Carry a light jacket from October to February — mornings on Deomali are cold.
        - Start early: sunrise at Deomali and morning light at the waterfalls are the highlights.
        - Mobile network can be weak in the hills, so share your plans before you go.
        - Respect local customs in tribal villages and ask before taking photographs.
        - Keep some cash for village markets and small shops.

        ## Explore Koraput with Ghumo Odisha
        The easiest way to see Koraput is on a planned group trip: transport, stays, meals and a day-wise itinerary are taken care of, and you travel with like-minded people. See our upcoming Koraput departures on the home page, or message us on WhatsApp to plan your trip.
        """,
        [
            "Koraput",
            "Koraput tourism",
            "Best places to visit in Koraput",
            "Koraput tourist places",
            "Places to visit in Koraput",
            "Things to do in Koraput",
            "Koraput sightseeing",
            "Koraput travel guide",
            "Koraput trip",
            "Koraput tour",
            "Koraput tour package",
            "Koraput trip from Bhubaneswar",
            "Koraput tour package from Bhubaneswar",
            "Koraput group tour",
            "Koraput weekend trip",
            "Koraput 3 days itinerary",
            "Koraput itinerary",
            "Best time to visit Koraput",
            "How to reach Koraput",
            "Koraput hill station",
            "Koraput waterfalls",
            "Koraput coffee",
            "Koraput coffee plantation",
            "Deomali",
            "Deomali hill",
            "Deomali trek",
            "Deomali sunrise",
            "Highest peak in Odisha",
            "Duduma waterfall",
            "Duduma waterfall Koraput",
            "Rani Duduma",
            "Gupteswar cave",
            "Gupteswar temple",
            "Gupta Kedar",
            "Kolab dam",
            "Upper Kolab reservoir",
            "Sabara Srikhetra",
            "Jagannath temple Koraput",
            "Tribal museum Koraput",
            "COATS museum",
            "Jeypore",
            "Jeypore tourist places",
            "Kotpad",
            "Kotpad handloom",
            "Koraput tribal culture",
            "Tribal tourism Odisha",
            "Eastern Ghats Odisha",
            "South Odisha tourism",
            "Hill stations in Odisha",
            "Waterfalls in Odisha",
            "Offbeat places in Odisha",
            "Odisha tourism",
            "Odisha tour package",
            "Odisha group tour",
            "Places to visit in Odisha",
            "Weekend getaways from Bhubaneswar",
            "Trips from Bhubaneswar",
            "Ghumo Odisha",
            "Ghumo Odisha Koraput",
        ]);

    public static readonly Story Mahendragiri = new(
        "Best Places to Visit in Mahendragiri: Trek, Temples & Travel Guide",
        "best-places-to-visit-in-mahendragiri",
        "Mahendragiri",
        "Ancient Pandava temples, a forest trek to the summit, Gandahati waterfall and the Jirang monastery — the best places to visit in Mahendragiri, when to go and how to plan your trip.",
        """
        Mahendragiri, in the Gajapati district of southern Odisha, is one of the most sacred and beautiful mountains in the state. At about 1,500 metres it is among the highest peaks in Odisha, wrapped in thick forest and crowned with temples linked to the Mahabharata. For trekkers, pilgrims and nature lovers alike, it is one of the most rewarding trips in Odisha.

        Here are the best places to visit in Mahendragiri and around it, with tips on the best time to go and how to plan your trip.

        ## 1. The Mahendragiri summit trek
        The trek to the top climbs through shaded forest, streams and boulder-strewn trails before opening up to wide views over the Eastern Ghats. It is a moderate trek that most reasonably fit travellers can do with an early start, a steady pace and plenty of water. On a clear day you can see ridge after ridge rolling to the horizon.

        ## 2. Kunti, Yudhisthira and Bhima temples
        The summit is famous for its ancient stone temples, which local tradition links to the Pandavas of the Mahabharata. The Kunti temple, the Yudhisthira temple and the Bhima temple stand among the trees, built of large stone blocks without mortar. Standing among them, high above the valleys, is a special experience.

        ## 3. Gokarneswar temple
        Gokarneswar, dedicated to Lord Shiva, is one of the most important shrines on Mahendragiri. It is the focus of the great Maha Shivaratri gathering, when thousands of devotees climb the mountain.

        ## 4. Forests, streams and biodiversity
        Mahendragiri is known for its rich biodiversity: dense forest, rare medicinal plants, orchids, butterflies and birds. The Mahendratanaya river begins in these hills. Walk slowly, listen and you will find something new at every turn.

        ## 5. Sunrise and sunset viewpoints
        The rocky viewpoints near the summit are perfect for sunrise and sunset. In the cooler months, mist rises from the valleys in the morning and the light turns the hills gold in the evening.

        ## 6. Gandahati waterfall
        Near Paralakhemundi, Gandahati is a lovely waterfall tumbling over rocks into pools surrounded by greenery. It is at its fullest after the monsoon and makes a refreshing stop on the way to or from Mahendragiri.

        ## 7. Jirang Monastery (Padmasambhava Mahavihara)
        In the hills of Chandragiri, the Jirang monastery is one of the largest Buddhist monasteries in eastern India, built by the Tibetan community that settled here. Its colourful halls, prayer flags and peaceful setting make it a favourite stop on Mahendragiri trips.

        ## 8. Paralakhemundi
        The district headquarters of Gajapati, Paralakhemundi is known for the old Gajapati palace and its blend of Odia and Telugu culture. It is a good place for a meal and a short heritage walk.

        ## Best time to visit Mahendragiri
        October to February is the best time to visit Mahendragiri: the weather is cool and pleasant for trekking, and the views are clear. Maha Shivaratri (February or March) is the most spiritual time to visit, but expect large crowds. During the monsoon (July to September) the forest is at its greenest, but trails are slippery.

        ## How many days do you need?
        Two to three days is ideal: one day for the summit trek and the temples, and another for Gandahati waterfall, the Jirang monastery and Paralakhemundi.

        ## How to reach Mahendragiri from Bhubaneswar
        Mahendragiri is about 300 km from Bhubaneswar by road, via Berhampur. Berhampur is the nearest major railway station and Bhubaneswar the nearest airport. The last part of the journey is on hill roads, so travelling with an experienced driver or a planned group trip makes it far easier.

        ## Trek tips
        - Start early to finish the climb before the midday heat.
        - Wear proper shoes with good grip; the trail is rocky in places.
        - Carry at least two litres of water, light snacks and a rain jacket.
        - Do not litter — carry all your waste back down the mountain.
        - The temples are sacred: dress modestly and follow local customs.

        ## Explore Mahendragiri with Ghumo Odisha
        Join a Ghumo Odisha group trip to Mahendragiri: transport from Bhubaneswar, stays, meals, a day-wise itinerary and trip coordinators are all taken care of. See our upcoming Mahendragiri departures on the home page, or message us on WhatsApp to plan your trip.
        """,
        [
            "Mahendragiri",
            "Mahendragiri Odisha",
            "Mahendragiri tourism",
            "Best places to visit in Mahendragiri",
            "Mahendragiri tourist places",
            "Places to visit in Mahendragiri",
            "Things to do in Mahendragiri",
            "Mahendragiri travel guide",
            "Mahendragiri trip",
            "Mahendragiri tour",
            "Mahendragiri tour package",
            "Mahendragiri trip from Bhubaneswar",
            "Mahendragiri tour package from Bhubaneswar",
            "Mahendragiri group tour",
            "Mahendragiri weekend trip",
            "Mahendragiri 2 days itinerary",
            "Mahendragiri itinerary",
            "Mahendragiri trek",
            "Mahendragiri trekking",
            "Mahendragiri hill",
            "Mahendragiri mountain",
            "Mahendragiri peak",
            "Mahendragiri temple",
            "Mahendragiri Pandava temples",
            "Kunti temple Mahendragiri",
            "Yudhisthira temple",
            "Bhima temple Mahendragiri",
            "Gokarneswar temple",
            "Mahendragiri Shivaratri",
            "Best time to visit Mahendragiri",
            "How to reach Mahendragiri",
            "Mahendragiri distance from Bhubaneswar",
            "Mahendragiri Gajapati",
            "Gajapati tourism",
            "Gajapati tourist places",
            "Paralakhemundi",
            "Paralakhemundi tourist places",
            "Gandahati waterfall",
            "Jirang monastery",
            "Chandragiri monastery",
            "Padmasambhava Mahavihara",
            "Mahendratanaya river",
            "Eastern Ghats Odisha",
            "Trekking in Odisha",
            "Treks near Bhubaneswar",
            "Hill stations in Odisha",
            "Highest peaks in Odisha",
            "Offbeat places in Odisha",
            "South Odisha tourism",
            "Odisha tourism",
            "Odisha tour package",
            "Odisha group tour",
            "Places to visit in Odisha",
            "Weekend getaways from Bhubaneswar",
            "Trips from Bhubaneswar",
            "Ghumo Odisha",
            "Ghumo Odisha Mahendragiri",
        ]);
}
