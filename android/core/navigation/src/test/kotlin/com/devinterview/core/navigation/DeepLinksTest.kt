package com.devinterview.core.navigation

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class DeepLinksTest {
    @Test fun `notification deep links map to routes`() {
        assertEquals(Routes.ChallengeDetail("abc"), DeepLinks.toRoute("devinterview://challenge/abc"))
        assertEquals(Routes.FlashcardDecks, DeepLinks.toRoute("devinterview://flashcards"))
        assertEquals(Routes.Home, DeepLinks.toRoute("devinterview://unknown-thing"))
        assertNull(DeepLinks.toRoute("https://evil.example.com"))
    }
}
